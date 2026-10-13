using System.Globalization;
using FluentValidation;
using MediFlow.Api.Authorization;
using MediFlow.Api.Endpoints;
using MediFlow.Api.Observability;
using MediFlow.BuildingBlocks.Application.Behaviors;
using MediFlow.Identity.Application.Authentication;
using MediFlow.Identity.Infrastructure;
using MediFlow.PatientProfile.Application.Patients.CreatePatient;
using MediFlow.PatientProfile.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

// Serilog'un kendi hatalarını (sink'e bağlanamadı, yapılandırma çözülemedi)
// görünür kıl. Sessiz yutulan loglama hatası, "loglarım neden boş?"
// sorusunu cevaplanamaz hale getirir.
Serilog.Debugging.SelfLog.Enable(Console.Error);

// InvariantCulture şart: Türkçe kültürde ondalık ayırıcı virgül olur
// ("13,86 ms") ve log analiz araçları sayıyı okuyamaz.
// Bootstrap logger: asıl logger yapılandırma okunurken kurulur; o aşamada
// çıkan hatalar (eksik bağlantı dizesi gibi) aksi halde hiçbir yere yazılmaz.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ========================================================
    //  MODÜLLER — her biri kendi DI kaydını yapıyor
    // ========================================================
    builder.Services.AddPatientProfileModule(builder.Configuration);
    builder.Services.AddIdentityModule(builder.Configuration);

    builder.Services.AddSingleton(TimeProvider.System);

    // ========================================================
    //  MEDIATOR VE PIPELINE — iki modülün handler'ları
    // ========================================================
    builder.Services.AddMediatR(configuration =>
    {
        configuration.RegisterServicesFromAssembly(typeof(CreatePatientCommand).Assembly);
        configuration.RegisterServicesFromAssembly(typeof(LoginCommand).Assembly);

        // SIRA: Logging dışta, Validation içte. Ters olsa doğrulamadan
        // reddedilen istekler loga hiç düşmezdi.
        configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
        configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));

        // Validation'dan SONRA = daha icte. Dogrulama basarisizsa handler
        // hic cagrilmadigi icin buraya da hic gelinmiyor - kaydedilecek bir
        // sey olmuyor. Generic kisiti sayesinde SADECE komutlarda calisiyor.
        configuration.AddOpenBehavior(typeof(UnitOfWorkBehavior<,>));
    });

    builder.Services.AddValidatorsFromAssembly(
        typeof(CreatePatientCommand).Assembly, includeInternalTypes: true);
    builder.Services.AddValidatorsFromAssembly(
        typeof(LoginCommand).Assembly, includeInternalTypes: true);

    // ========================================================
    //  KİMLİK DOĞRULAMA — gelen JWT'yi doğrula
    // ========================================================
    // Doğrulama ayarları JwtBearerOptionsSetup'ta ve IOptions üzerinden
    // TEMBEL çözülüyor.
    //
    // Burada yapılandırmayı doğrudan okumak (GetSection().Get<T>()) bir
    // hataya yol açmıştı: o okuma değeri O AN donduruyor, jeton üretimi
    // ise IOptions ile sonradan okuyordu. Integration testinde fabrika
    // yapılandırmayı sonradan ezdiği için ikisi FARKLI anahtar gördü ve
    // her istek 401 aldı. IOptions, tüm bileşenlerin aynı değeri
    // görmesini garanti ediyor.
    //
    // Eksik yapılandırma kontrolü de burada değil: IdentityModule'daki
    // AddOptions<JwtOptions>().Validate(...).ValidateOnStart() yapıyor.
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();

    builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();

    // IZIN BAZLI YETKILENDIRME
    //
    // PermissionPolicyProvider politikalari ihtiyac aninda uretiyor, yani
    // her yeni izin icin baslangicta kayit yazmak gerekmiyor.
    // Singleton: durum tutmuyor ve her istekte cagriliyor.
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

    builder.Services.AddAuthorization();

    // ========================================================
    //  HTTP
    // ========================================================
    builder.Services.AddOpenApi();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    // ---- Gelistirme ortaminda sema ve tohum verisi ----
    if (app.Environment.IsDevelopment())
    {
        // Migration'lari ACILISTA uygula.
        //
        // NEDEN SADECE GELISTIRMEDE? Uretimde birden fazla uygulama ornegi
        // ayni anda acilir ve hepsi birden migration uygulamaya calisirsa
        // yarisirlar. Uretimde migration, dagitim hattinda AYRI bir adim
        // olarak ve TEK kez calistirilir (Faz 7'de boyle yapacagiz).
        // Gelistirmede ise "klonla ve calistir" deneyimi daha degerli.
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<PatientProfileDbContext>()
                .Database.MigrateAsync();

            await scope.ServiceProvider
                .GetRequiredService<IdentityDbContext>()
                .Database.MigrateAsync();
        }

        // Sifirdan kurulan sistemde hic yetkili kullanici olmaz: kendi
        // kaydolan kullanici "Patient" rolu alir ve hasta YAZMA izni yoktur
        // (dogru olan bu). Ilk yoneticiyi tohumluyoruz. Idempotent.
        await IdentitySeeder.SeedAdministratorAsync(app.Services);
    }

    // ---- Middleware sırası ----
    app.UseExceptionHandler();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            diagnosticContext.Set(
                CorrelationIdMiddleware.ItemKey,
                httpContext.Items[CorrelationIdMiddleware.ItemKey]));

    // SIRA ZORUNLU: önce "kimsin" (authentication), sonra "yetkin var mı"
    // (authorization). Kimliği bilinmeyenin yetkisi sorgulanamaz.
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapGet("/", () => Results.Redirect("/scalar"))
       .ExcludeFromDescription();

    app.MapAuthEndpoints();
    app.MapPatientEndpoints();

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application terminated unexpectedly during startup");
    throw;
}
finally
{
    // Serilog toplu gönderim yapıyor; bu çağrı olmadan kapanış anındaki
    // son loglar Seq'e hiç ulaşmaz.
    Log.CloseAndFlush();
}
