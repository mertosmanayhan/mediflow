using System.Text;
using MediFlow.BuildingBlocks.Application;
using MediFlow.Identity.Application.Abstractions;
using MediFlow.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediFlow.Identity.Infrastructure;

/// <summary>Identity modülünün kendi DI kayıtları.</summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // AYRI VERİTABANI, ayrı bağlantı dizesi.
        // Aynı sunucuda ama ayrı veritabanında: modül Faz 2'de kendi
        // servisine çıkarıldığında veritabanını taşımak yerine sadece
        // bağlantı dizesini yeni servise vermek yeterli olacak.
        string connectionString = configuration.GetConnectionString("Identity")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Identity tanımlı değil. " +
                "dotnet user-secrets set \"ConnectionStrings:Identity\" \"...\" " +
                "--project src/Host/MediFlow.Api");

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServer =>
            {
                sqlServer.MigrationsHistoryTable("__EFMigrationsHistory", "Identity");
                sqlServer.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            }));

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();

        // Identity'nin DbContext'i de bir unit of work.
        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<IdentityDbContext>());

        // Hash'leyici durum tutmuyor ve thread-safe -> tek örnek yeter.
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // Jeton üreticisi de durumsuz; yapılandırmayı IOptions ile alıyor.
        services.AddSingleton<ITokenProvider, JwtTokenProvider>();

        // OPTIONS DOĞRULAMASI + ValidateOnStart.
        //
        // Configure<T>() sessizdir: yapılandırma eksik veya bağlanamamışsa
        // nesne varsayılanlarla kalır ve hata çok sonra, alakasız bir
        // belirtiyle çıkar (bizde 401 olarak çıktı).
        //
        // AddOptions<T>().Validate(...).ValidateOnStart() ile uygulama
        // AÇILIRKEN patlıyor ve ne eksik olduğunu söylüyor.
        // Faz 0 ilkesi: gürültülü hata > sessiz yanlış davranış.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer),
                "Jwt:Issuer tanımlı değil.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Audience),
                "Jwt:Audience tanımlı değil.")
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.SigningKey) >= JwtOptions.MinimumSigningKeyBytes,
                $"Jwt:SigningKey en az {JwtOptions.MinimumSigningKeyBytes} bayt olmalı (HMAC-SHA256 gereği).")
            .ValidateOnStart();

        return services;
    }
}
