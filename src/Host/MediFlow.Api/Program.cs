using MediFlow.Api;
using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Application;
using MediFlow.PatientProfile.Domain;
using MediFlow.PatientProfile.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
//  BAĞIMLILIK KAYDI (Dependency Injection)
//
//  CreatePatientHandler'ın kurucusu iki şey istiyordu:
//  bir IPatientRepository ve bir TimeProvider. Onları kimin
//  vereceğini burada söylüyoruz.
//
//  Sonra bir endpoint "bana CreatePatientHandler ver" dediğinde
//  ASP.NET Core bu listeye bakıp gerekli parçaları kendisi
//  oluşturup birleştiriyor. Hiçbir yerde elle 'new' yazmıyoruz.
//
//  ÖMÜR (lifetime) seçimleri:
// ============================================================

// Singleton: uygulama boyunca TEK örnek.
// Hafızadaki depo için ZORUNLU - her istek kendi deposunu alsaydı
// kaydettiğin hasta bir sonraki istekte kaybolurdu.
// (Veritabanına geçtiğimizde bu Scoped olacak; nedenini o zaman göreceğiz.)
builder.Services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();

// Gerçek sistem saati. Testlerde bunun yerine sabit saat veriyorduk;
// aynı arayüz, farklı dolduran.
builder.Services.AddSingleton(TimeProvider.System);

// Scoped: her HTTP isteği için bir örnek. İş akışları durum tutmadığı
// için ucuz; istek bitince atılır.
builder.Services.AddScoped<CreatePatientHandler>();

// API'nin kendini tanımlayan OpenAPI belgesini üretir.
builder.Services.AddOpenApi();

var app = builder.Build();

// /openapi/v1.json adresinde makine okunabilir API tanımı
app.MapOpenApi();

// /scalar adresinde insan için gezilebilir arayüz.
// Buradan POST isteği gönderip deneyebiliyoruz - tarayıcı adres
// çubuğundan sadece GET yapılabilir.
app.MapScalarApiReference();

// Kök adres, ne yapması gerektiğini söylesin.
app.MapGet("/", () => Results.Redirect("/scalar"))
   .ExcludeFromDescription();

// ============================================================
//  ENDPOINT: yeni hasta oluştur
// ============================================================
app.MapPost("/patients", async (
        CreatePatientRequest request,
        CreatePatientHandler handler,
        CancellationToken cancellationToken) =>
    {
        Result<Guid> result = await handler.HandleAsync(request, cancellationToken);

        // Başarıda 201 Created ve yeni kaynağın adresi Location başlığında.
        // 200 değil 201: HTTP'de "yeni bir şey oluştu"nun karşılığı bu.
        // Hatada, hata kodundan durum koduna eşleme ErrorMapping'de yapılır.
        return result.IsSuccess
            ? Results.Created($"/patients/{result.Value}", new { id = result.Value })
            : result.Error.ToHttpProblem();
    })
    .WithName("CreatePatient")
    .WithSummary("Yeni hasta kaydı oluşturur");

// ============================================================
//  ENDPOINT: hastayı kimliğe göre getir
// ============================================================
// {id:guid} -> yol kısıtı. Adres /patients/abc gibi geçersiz bir değer
// içeriyorsa endpoint hiç çalışmaz, doğrudan 404 döner. Guid.Parse
// hatasını elle yakalamak gerekmiyor.
app.MapGet("/patients/{id:guid}", async (
        Guid id,
        IPatientRepository patients,
        CancellationToken cancellationToken) =>
    {
        Patient? patient = await patients.GetByIdAsync(id, cancellationToken);

        // Başarı durumunda domain nesnesini değil, DTO'yu döndürüyoruz.
        return patient is null
            ? PatientErrors.NotFound.ToHttpProblem()
            : Results.Ok(PatientResponse.From(patient));
    })
    .WithName("GetPatientById")
    .WithSummary("Kimliğe göre hasta getirir");

app.Run();
