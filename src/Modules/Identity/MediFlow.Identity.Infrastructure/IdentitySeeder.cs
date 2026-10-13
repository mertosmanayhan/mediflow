using MediFlow.BuildingBlocks.Common;
using MediFlow.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediFlow.Identity.Infrastructure;

/// <summary>
/// Sistemde hiç yönetici yoksa bir tane oluşturur.
/// </summary>
/// <remarks>
/// NEDEN GEREKLİ?
/// İzin bazlı yetkilendirme devreye girince bir tavuk-yumurta problemi
/// doğdu: kendi kaydolan kullanıcı "Patient" rolü alıyor ve bu rolün
/// hasta YAZMA izni yok (doğru olan bu - hasta, hasta kaydı oluşturmaz).
/// Dolayısıyla sıfırdan kurulan bir sistemde hiç kimse veri giremez.
///
/// Çözüm: ilk yöneticiyi tohumlamak. Gerçek sistemlerde de böyle yapılır.
///
/// GÜVENLİK KARARLARI:
///   - Şifre YAPILANDIRMADAN gelir, kodda sabit değildir. Kodda sabit bir
///     yönetici şifresi, depoyu okuyan herkese sistemi açar - gerçek
///     ihlallerin klasik sebeplerinden biri.
///   - Şifre tanımlı değilse tohumlama ATLANIR. Varsayılan bir şifre
///     koymak daha kötüsü olurdu: üretime çıkan sistemde kimsenin
///     değiştirmediği bilinen bir şifre kalır.
///   - Yönetici ZATEN varsa hiçbir şey yapılmaz; çalıştırmak güvenli
///     (idempotent).
/// </remarks>
public static class IdentitySeeder
{
    public const string DefaultAdminEmail = "admin@mediflow.local";

    public static async Task SeedAdministratorAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = services.CreateScope();

        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        ILogger logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(IdentitySeeder));

        string? password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(password))
        {
            SeedMessages.Skipped(logger);
            return;
        }

        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        bool administratorExists = await context.Users
            .AnyAsync(user => user.Roles.Contains(RolePermissions.Administrator), cancellationToken);

        if (administratorExists)
        {
            return;
        }

        string? email = configuration["Seed:AdminEmail"] ?? DefaultAdminEmail;

        Result<Email> adminEmail = Email.Create(email);

        if (adminEmail.IsFailure)
        {
            SeedMessages.InvalidEmail(logger, email);
            return;
        }

        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        TimeProvider clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        Result<User> administrator = User.Create(
            adminEmail.Value,
            hasher.Hash(password),
            clock.GetUtcNow(),
            roles: [RolePermissions.Administrator]);

        if (administrator.IsFailure)
        {
            SeedMessages.Failed(logger, administrator.Error.Code);
            return;
        }

        context.Users.Add(administrator.Value);
        await context.SaveChangesAsync(cancellationToken);

        SeedMessages.Created(logger, adminEmail.Value.Value);
    }
}

internal static partial class SeedMessages
{
    [LoggerMessage(EventId = 6000, Level = LogLevel.Warning,
        Message = "Administrator seeding skipped: Seed:AdminPassword is not configured")]
    public static partial void Skipped(ILogger logger);

    [LoggerMessage(EventId = 6001, Level = LogLevel.Information,
        Message = "Seeded administrator {Email}")]
    public static partial void Created(ILogger logger, string email);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Error,
        Message = "Administrator seeding failed: invalid email {Email}")]
    public static partial void InvalidEmail(ILogger logger, string email);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Error,
        Message = "Administrator seeding failed: {ErrorCode}")]
    public static partial void Failed(ILogger logger, string errorCode);
}
