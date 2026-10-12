using System.Globalization;
using FluentValidation;
using MediatR;
using MediFlow.Api;
using MediFlow.Api.Observability;
using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Application.Behaviors;
using MediFlow.PatientProfile.Application.Patients.CreatePatient;
using MediFlow.PatientProfile.Application.Patients.GetPatientById;
using MediFlow.PatientProfile.Domain;
using MediFlow.PatientProfile.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

// ============================================================
//  ÖNYÜKLEME LOGGER'I (bootstrap logger)
//
//  İki aşamalı kurulum. Nedeni: asıl logger yapılandırmayı okuyarak
//  kurulur, ama yapılandırma OKUNURKEN de hata çıkabilir (eksik bağlantı
//  dizesi gibi - EF adımında tam bunu yaşadık). O anda henüz logger
//  olmadığı için hata hiçbir yere yazılmaz.
//
//  Bu basit logger o boşluğu kapatıyor: uygulama ayağa kalkamasa bile
//  nedeni loglanmış oluyor. Üretimde "servis açılmıyor ve sebebi
//  bilinmiyor" durumunu engelliyor.
// ============================================================
// LOGLAMA HATALARINI GÖRÜNÜR KIL.
// Serilog, kendi içindeki hataları (sink'e bağlanamadı, yapılandırma
// çözülemedi, olay gönderilemedi) SESSİZCE yutar - mantığı şu: loglama
// uygulamayı çökertmemeli. Ama sessizlik, "loglarım neden boş?" sorusunu
// cevaplanamaz hale getirir. SelfLog o hataları stderr'e yazar.
Serilog.Debugging.SelfLog.Enable(Console.Error);

// InvariantCulture şart: Türkçe kültürde ondalık ayırıcı virgül olur
// ("13,86 ms") ve log analiz araçları sayıyı okuyamaz. Loglar makineler için
// yazılır, kültürden bağımsız olmak zorundadır.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Asıl logger: appsettings.json'daki Serilog bölümünden kurulur.
    // Yapılandırmanın kodda değil dosyada olması, log seviyesini yeniden
    // derlemeden değiştirebilmek demek (üretimde gerekli).
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ========================================================
    //  VERİTABANI
    // ========================================================
    string connectionString = builder.Configuration.GetConnectionString("PatientProfile")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:PatientProfile tanımlı değil. Yerel geliştirme için: " +
            "dotnet user-secrets set \"ConnectionStrings:PatientProfile\" \"...\" " +
            "--project src/Host/MediFlow.Api");

    builder.Services.AddDbContext<PatientProfileDbContext>(options =>
        options.UseSqlServer(connectionString, sqlServer =>
        {
            sqlServer.MigrationsHistoryTable("__EFMigrationsHistory", "PatientProfile");

            // Geçici hata dayanıklılığı: bulutta bağlantı düzenli olarak
            // kopar (ölçekleme, bakım, ağ). Bunlar arıza değil, normal
            // işletim koşulu.
            //
            // ⚠️ Retry açıkken ELLE transaction açmak hata verir; işlemi
            // context.Database.CreateExecutionStrategy() içinde sarmak
            // gerekir. Transaction behavior'ını yazarken karşımıza çıkacak.
            sqlServer.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        }));

    builder.Services.AddScoped<IPatientRepository, EfPatientRepository>();
    builder.Services.AddSingleton(TimeProvider.System);

    // ========================================================
    //  MEDIATOR VE PIPELINE
    // ========================================================
    builder.Services.AddMediatR(configuration =>
    {
        configuration.RegisterServicesFromAssembly(typeof(CreatePatientCommand).Assembly);

        // SIRA: Logging dışta, Validation içte. Ters olsa doğrulamadan
        // reddedilen istekler loga hiç düşmezdi.
        configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
        configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
    });

    builder.Services.AddValidatorsFromAssembly(
        typeof(CreatePatientCommand).Assembly,
        includeInternalTypes: true);

    // ========================================================
    //  HTTP
    // ========================================================
    builder.Services.AddOpenApi();

    // Yakalanmamış istisnalar için tek merkez. AddProblemDetails,
    // çerçevenin ürettiği hata cevaplarını da RFC 9457 biçimine çeviriyor.
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    // ========================================================
    //  MIDDLEWARE SIRASI — önemlidir, yukarıdan aşağı çalışır
    // ========================================================

    // 1) En dışta istisna işleyicisi: altındaki her şeyi sarıyor.
    //    Geliştirme modunda da aktif - böylece "üretimde başka davranıyor"
    //    sürprizi olmuyor ve yığın izi asla cevaba sızmıyor.
    app.UseExceptionHandler();

    // 2) İzleme kimliği: bundan sonraki her log satırına eklenecek.
    app.UseMiddleware<CorrelationIdMiddleware>();

    // 3) İstek logu: her HTTP isteği için tek bir özet satır
    //    (metot, yol, durum kodu, süre). ASP.NET Core'un varsayılan
    //    gürültülü üç satırı yerine tek yapılandırılmış satır.
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set(
                CorrelationIdMiddleware.ItemKey,
                httpContext.Items[CorrelationIdMiddleware.ItemKey]);
        });

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapGet("/", () => Results.Redirect("/scalar"))
       .ExcludeFromDescription();

    // ========================================================
    //  ENDPOINT'LER
    //  Endpoint'ler handler'ı değil ISender'ı istiyor; hangi handler'ın
    //  çalışacağını bilmiyorlar.
    // ========================================================

    // Komut doğrudan JSON gövdesinden bağlanıyor; yani komut aynı zamanda
    // dış sözleşme. Tek istemcimiz olduğu için ayrı bir istek tipi + eşleme
    // tekrarını yapmıyoruz.
    app.MapPost("/patients", async (
            CreatePatientCommand command,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/patients/{result.Value}", new { id = result.Value })
                : result.Error.ToHttpProblem();
        })
        .WithName("CreatePatient")
        .WithSummary("Yeni hasta kaydı oluşturur");

    app.MapGet("/patients/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result<PatientDto> result = await sender.Send(
                new GetPatientByIdQuery(id), cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.Error.ToHttpProblem();
        })
        .WithName("GetPatientById")
        .WithSummary("Kimliğe göre hasta getirir");

    app.Run();
}
catch (Exception exception)
{
    // Uygulama hiç ayağa kalkamadıysa son söz burası.
    Log.Fatal(exception, "Application terminated unexpectedly during startup");
    throw;
}
finally
{
    // Serilog arka planda toplu gönderim yapıyor. Bu çağrı olmadan
    // kapanış anındaki son loglar Seq'e hiç ulaşmaz - özellikle
    // çöküş anındaki en kritik satırlar.
    Log.CloseAndFlush();
}
