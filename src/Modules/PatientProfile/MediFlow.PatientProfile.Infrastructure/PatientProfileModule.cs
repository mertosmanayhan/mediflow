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
/// </remarks>
public static class PatientProfileModule
{
    public static IServiceCollection AddPatientProfileModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("PatientProfile")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:PatientProfile tanımlı değil. " +
                "dotnet user-secrets set \"ConnectionStrings:PatientProfile\" \"...\" " +
                "--project src/Host/MediFlow.Api");

        services.AddDbContext<PatientProfileDbContext>(options =>
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
            }));

        services.AddScoped<IPatientRepository, EfPatientRepository>();

        return services;
    }
}
