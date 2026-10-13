using MediFlow.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediFlow.Identity.Infrastructure;

/// <remarks>
/// SaveChanges ARTIK YOK. Yazma işini pipeline'daki UnitOfWorkBehavior
/// yapıyor; repository'nin işi değişikliği bildirmek.
///
/// Bu değişiklik en çok yenileme akışında işe yarıyor: orada TEK istekte
/// eski jeton iptal ediliyor VE yenisi ekleniyor. Önceden bunlar ayrı ayrı
/// yazılıyordu - arada bir hata olsa eski jeton iptal, yeni jeton yok
/// kalırdı ve kullanıcı oturumunu tamamen kaybederdi. Artık ikisi tek
/// SaveChanges ile, yani tek transaction'da yazılıyor.
/// </remarks>
internal sealed class EfUserRepository(IdentityDbContext context) : IUserRepository
{
    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        return Task.CompletedTask;
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        await context.Users
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Users
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        await context.Users
            .AnyAsync(user => user.Email == email, cancellationToken);
}

internal sealed class EfRefreshTokenRepository(IdentityDbContext context) : IRefreshTokenRepository
{
    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        context.RefreshTokens.Add(token);
        return Task.CompletedTask;
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        List<RefreshToken> active = await context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (RefreshToken token in active)
        {
            token.Revoke(nowUtc);
        }
    }
}
