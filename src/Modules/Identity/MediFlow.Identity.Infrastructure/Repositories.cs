using MediFlow.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediFlow.Identity.Infrastructure;

internal sealed class EfUserRepository(IdentityDbContext context) : IUserRepository
{
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
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

    /// <summary>
    /// Bekleyen değişiklikleri yazar.
    /// </summary>
    /// <remarks>
    /// NEDEN AddAsync İÇİNDE SaveChanges ÇAĞRILMIYOR?
    /// Rotasyon akışı TEK bir işlemde iki şey yapıyor: eski jetonu iptal
    /// etmek ve yenisini eklemek. İkisi ayrı ayrı yazılırsa arada bir hata
    /// olursa tutarsız durum kalır (eski iptal, yeni yok -> kullanıcı
    /// oturumunu kaybeder).
    ///
    /// Bu, Unit of Work deseninin ilk somut ihtiyacı. Şimdilik kayıt
    /// sınıfı SaveChanges'i ayrı bir metot olarak sunuyor; Blok 5'te bunu
    /// pipeline'daki bir transaction behavior'a taşıyacağız ve handler'lar
    /// SaveChanges'i hiç çağırmayacak.
    /// </remarks>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);
}
