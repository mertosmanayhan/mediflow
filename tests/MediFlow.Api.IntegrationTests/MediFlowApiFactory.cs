using MediFlow.Identity.Infrastructure;
using MediFlow.PatientProfile.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediFlow.Api.IntegrationTests;

/// <summary>
/// Uygulamayı test için bellekte ayağa kaldırır.
/// </summary>
/// <remarks>
/// NE TEST EDİYORUZ?
/// Birim testler parçaları tek tek doğruluyor. Ama parçalar DOĞRU
/// BAĞLANDIĞINI kanıtlamıyor: DI kaydı eksikse, middleware sırası
/// yanlışsa, yetkilendirme politikası kurulmamışsa birim testler yine
/// yeşil kalır. Bunlar sadece gerçek pipeline çalıştığında ortaya çıkar.
///
/// Gerçek ağ soketi açılmıyor (daha hızlı ve port çakışması yok) ama
/// middleware, DI, kimlik doğrulama, yetkilendirme, model bağlama,
/// EF ve veritabanı - hepsi gerçek.
///
/// AYRI VERİTABANI: testler geliştirme verisini kirletmemeli, ve
/// geliştirme verisi testleri etkilememeli. Faz 6'da Testcontainers ile
/// veritabanını testin kendisi ayağa kaldıracak; şimdilik aynı SQL Server
/// üzerinde ayrı veritabanları yeterli.
/// </remarks>
public sealed class MediFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // 127.0.0.1, "localhost" DEĞİL: CI runner'ında localhost önce IPv6
    // (::1) olarak çözülebiliyor, service container'ın yayınladığı port
    // ise IPv4'te duruyor - sonuç "connection refused". Adresi sabitlemek
    // bu belirsizliği ortadan kaldırıyor.
    private const string ServerConnection = "Server=127.0.0.1,1433;User Id=sa;Password=MediFlow_Dev2026!;TrustServerCertificate=True;Encrypt=True";

    internal const string AdminEmail = "admin@mediflow.test";
    internal const string AdminPassword = "YoneticiTestSifresi2026!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // AddInMemoryCollection EN SONA eklendiği için en yüksek önceliğe
        // sahip: user-secrets ve appsettings değerlerini eziyor. Testin
        // geliştirme veritabanına dokunmaması bu sayede garanti.
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PatientProfile"] = $"{ServerConnection};Database=MediFlow.PatientProfile.Tests",
                ["ConnectionStrings:Identity"] = $"{ServerConnection};Database=MediFlow.Identity.Tests",

                // Testin kendi imzalama anahtarı: üretim/geliştirme anahtarı
                // test koduna hiç girmiyor.
                ["Jwt:SigningKey"] = "test-only-signing-key-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "MediFlow.Api",
                ["Jwt:Audience"] = "MediFlow.Api",

                ["Seed:AdminEmail"] = AdminEmail,
                ["Seed:AdminPassword"] = AdminPassword,

                // Test çıktısını log gürültüsüyle doldurmamak için.
                ["Serilog:MinimumLevel:Default"] = "Warning",
            }));
    }

    public async Task InitializeAsync()
    {
        using IServiceScope scope = Services.CreateScope();

        // Şemayı migration'larla kuruyoruz, EnsureCreated ile DEĞİL.
        // Sebebi: EnsureCreated migration'ları atlar, yani testler
        // üretimde çalışacak şemadan FARKLI bir şema üzerinde koşar ve
        // migration hatalarını hiç yakalamaz. Böyle bir test yanlış
        // güven verir.
        await scope.ServiceProvider
            .GetRequiredService<PatientProfileDbContext>()
            .Database.MigrateAsync();

        await scope.ServiceProvider
            .GetRequiredService<IdentityDbContext>()
            .Database.MigrateAsync();
    }

    public new Task DisposeAsync() => Task.CompletedTask;
}
