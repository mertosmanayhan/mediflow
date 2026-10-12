using FluentValidation;
using MediatR;
using MediFlow.Api;
using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Application.Behaviors;
using MediFlow.PatientProfile.Application.Patients.CreatePatient;
using MediFlow.PatientProfile.Application.Patients.GetPatientById;
using MediFlow.PatientProfile.Domain;
using MediFlow.PatientProfile.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
//  VERİTABANI
// ============================================================
string connectionString = builder.Configuration.GetConnectionString("PatientProfile")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:PatientProfile tanımlı değil. Yerel geliştirme için: " +
        "dotnet user-secrets set \"ConnectionStrings:PatientProfile\" \"...\" " +
        "--project src/Host/MediFlow.Api");

builder.Services.AddDbContext<PatientProfileDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServer =>
    {
        sqlServer.MigrationsHistoryTable("__EFMigrationsHistory", "PatientProfile");

        // GEÇİCİ HATA DAYANIKLILIĞI (transient fault handling).
        //
        // Veritabanı bağlantısı üretimde DÜZENLİ olarak kopar: ölçekleme,
        // bakım penceresi, geçici ağ sorunu, bulut sağlayıcısının altyapı
        // değişikliği. Bunlar arıza değil, normal işletim koşulu.
        //
        // Bu ayar, kopan bağlantıyı belirli sayıda ve artan beklemelerle
        // (exponential backoff) yeniden dener. Yani her ağ hıçkırığı
        // kullanıcıya 500 olarak dönmez.
        //
        // ⚠️ GELEN TUZAK: retry açıkken ELLE transaction açarsan
        // (BeginTransaction) EF hata verir. Sebebi mantıklı: yeniden
        // denenen bir işlem transaction'ın neresinden başlayacağını
        // bilemez. Çözüm, işlemi execution strategy içinde sarmak:
        //
        //     var strategy = context.Database.CreateExecutionStrategy();
        //     await strategy.ExecuteAsync(async () => { ... });
        //
        // Bu, sıradaki transaction behavior'ını yazarken karşımıza çıkacak.
        sqlServer.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    }));

builder.Services.AddScoped<IPatientRepository, EfPatientRepository>();
builder.Services.AddSingleton(TimeProvider.System);

// ============================================================
//  MEDIATOR VE PIPELINE
// ============================================================
builder.Services.AddMediatR(configuration =>
{
    // Handler'ları assembly taramasıyla bulur. Yeni bir komut eklediğinde
    // burayı GÜNCELLEMEN GEREKMİYOR - dosyayı yazmak yeterli.
    //
    // Bedeli: kayıt çalışma zamanında yapılıyor, derleme zamanında değil.
    // Handler'ı yazmayı unutursan derleyici uyarmaz; hata isteği
    // gönderdiğinde çıkar. Bu, mediator yaklaşımının gerçek bir maliyeti.
    configuration.RegisterServicesFromAssembly(typeof(CreatePatientCommand).Assembly);

    // ⚠️ SIRA ÖNEMLİ. Behavior'lar KAYIT SIRASINA göre iç içe geçiyor:
    //
    //   Logging  (en dışta)
    //     └── Validation
    //           └── Handler
    //
    // Logging'i dışa koymamızın sebebi: ölçtüğü süre doğrulamayı DA
    // kapsasın ve doğrulamada reddedilen istekler de loglanmış olsun.
    // Ters sırada olsa, doğrulamadan dönen istekler loga hiç düşmezdi -
    // yani "kullanıcı neden hata alıyor?" sorusunu cevaplayamazdık.
    configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
    configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// Doğrulayıcıları da assembly taramasıyla bul.
// includeInternalTypes: internal doğrulayıcılar da bulunsun.
builder.Services.AddValidatorsFromAssembly(
    typeof(CreatePatientCommand).Assembly,
    includeInternalTypes: true);

// ============================================================
//  HTTP
// ============================================================
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/", () => Results.Redirect("/scalar"))
   .ExcludeFromDescription();

// ============================================================
//  ENDPOINT'LER
//
//  Dikkat: endpoint'ler artık handler'ı DEĞİL, ISender'ı istiyor.
//  Hangi handler'ın çalışacağını bilmiyorlar - komutu/sorguyu
//  gönderip sonucu HTTP'ye çeviriyorlar. Api katmanının işi bu kadar.
// ============================================================

// Komut doğrudan JSON gövdesinden bağlanıyor. Yani komut aynı zamanda dış
// sözleşme. Alternatif ayrı bir istek tipi + eşleme olurdu; tek istemcimiz
// olduğu için bu tekrarı yapmıyoruz.
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
        Result<PatientDto> result = await sender.Send(new GetPatientByIdQuery(id), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpProblem();
    })
    .WithName("GetPatientById")
    .WithSummary("Kimliğe göre hasta getirir");

app.Run();
