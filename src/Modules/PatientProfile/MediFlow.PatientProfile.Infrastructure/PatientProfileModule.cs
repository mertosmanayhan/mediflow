using MediFlow.BuildingBlocks.Application;
using MediFlow.BuildingBlocks.Infrastructure;
using MediFlow.PatientProfile.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediFlow.PatientProfile.Infrastructure;

/// <summary>
/// PatientProfile modülünün kendi DI kayıtları.
/// </summary>
/// <remarks>
/// MODÜL KAYIT DESENİ (module registration).
///
/// Program.cs'in her modülün iç detaylarını bilmesi yerine, her modül
/// kendi kaydını sunuyor. Faydaları:
///   - Program.cs modül sayısı arttıkça büyümüyor
///   - Bir modülün bağımlılıkları tek yerde toplu görünüyor
///   - Faz 2'de modül ayrı servise çıkarıldığında bu dosya olduğu gibi
///     yeni servisin Program.cs'ine taşınıyor
///
/// Bu, "composition root" fikrinin modüler hali: nesne grafiği tek bir
/// yerde kurulur, ama her modül kendi parçasını tanımlar.
///
/// ⚠️ BU METOT NEDEN <c>IConfiguration</c> ALMIYOR?
///
/// Önce alıyordu ve bağlantı dizesini kayıt anında okuyordu. Bu HEMEN
/// (eager) bir okumadır ve iki hataya yol açtı:
///
///  1. CI'da bağlantı dizesi user-secrets'ta olduğu için okunamıyor ve
///     uygulama hiç ayağa kalkmıyordu.
///  2. Daha sinsisi: <c>WebApplicationFactory</c> yapılandırmayı Program
///     bu satırı çalıştırdıktan SONRA eziyor. Yani entegrasyon testleri
///     testin verdiği bağlantı dizesini değil, GELİŞTİRME veritabanını
///     kullanıyordu. Testler yeşildi - ama yanlış veritabanında.
///
/// Çözüm: bağlantı dizesi <c>AddDbContext</c> lambda'sının İÇİNDE,
/// servis sağlayıcıdan <c>IConfiguration</c> çözülerek okunuyor. O
/// lambda DbContext ilk kez istendiğinde çalışıyor, yani yapılandırma
/// artık kesinleşmiş durumda.
///
/// Metodun <c>IConfiguration</c> parametresini KALDIRMAK bilinçli:
/// almadığın bir şeyi çok erken okuyamazsın. Hatayı imkânsız kılmak,
/// hatırlamaya çalışmaktan güvenli.
/// </remarks>
public static class PatientProfileModule
{
    public static IServiceCollection AddPatientProfileModule(this IServiceCollection services)
    {
        // Interceptor'lar tekil: durum tutmuyorlar, sadece TimeProvider
        // alıyorlar.
        services.AddSingleton<AuditingInterceptor>();
        services.AddSingleton<SoftDeleteInterceptor>();

        services.AddDbContext<PatientProfileDbContext>((serviceProvider, options) =>
        {
            // Audit alanlarını doldurma ve silme->güncelleme çevirimi
            // buradan devreye giriyor. Hiçbir handler bunları bilmiyor.
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditingInterceptor>(),
                serviceProvider.GetRequiredService<SoftDeleteInterceptor>());

            string connectionString = serviceProvider
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("PatientProfile")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:PatientProfile tanımlı değil. " +
                    "dotnet user-secrets set \"ConnectionStrings:PatientProfile\" \"...\" " +
                    "--project src/Host/MediFlow.Api");

            options.UseSqlServer(connectionString, sqlServer =>
            {
                sqlServer.MigrationsHistoryTable("__EFMigrationsHistory", "PatientProfile");

                // Bulutta bağlantı kopması normal işletim koşulu.
                // ⚠️ Retry açıkken elle transaction açmak için execution
                // strategy gerekir - Blok 5'te karşımıza çıkacak.
                sqlServer.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        });

        services.AddScoped<IPatientRepository, EfPatientRepository>();

        // Bu modülün DbContext'i bir unit of work olarak da kaydediliyor.
        // Pipeline'daki UnitOfWorkBehavior IEnumerable<IUnitOfWork> alıyor
        // ve kayıtlı tüm modüllerinkini topluyor.
        //
        // AYNI örneği döndürüyoruz (yeni bir DbContext değil): aksi halde
        // handler'ın değişiklik yaptığı oturum ile kaydedilen oturum farklı
        // olur ve hiçbir şey yazılmaz - sessiz ve teşhisi zor bir hata.
        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<PatientProfileDbContext>());

        return services;
    }
}
