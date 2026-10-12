using MediFlow.BuildingBlocks.Common;

namespace MediFlow.Identity.Domain;

/// <summary>
/// Uzun ömürlü yenileme jetonu kaydı.
/// </summary>
/// <remarks>
/// NEDEN GEREKLİ?
/// Access token (JWT) sunucuda saklanmaz; imzası doğrulanır ve kabul
/// edilir. Bu onu hızlı yapar ama bir sorunu var: GERİ ALINAMAZ. Süresi
/// bitene kadar geçerlidir, kullanıcı çıkış yapsa bile.
///
/// Çözüm iki jetonlu yapı:
///   - Access token: KISA ömürlü (15 dk). Çalınsa bile penceresi küçük.
///   - Refresh token: UZUN ömürlü (7 gün) ama VERİTABANINDA saklanır,
///     dolayısıyla İPTAL EDİLEBİLİR.
///
/// NEDEN JETONUN KENDİSİ DEĞİL HASH'İ SAKLANIYOR?
/// Refresh token da bir kimlik bilgisidir - şifre gibi. Veritabanı
/// sızarsa düz metin jetonlarla saldırgan doğrudan oturum açabilir.
/// Hash saklanırsa sızan veri işe yaramaz. Şifreyi hash'liyorsak jetonu
/// da hash'lemeliyiz; aynı mantık.
///
/// ROTASYON: Her yenilemede bu kayıt "kullanıldı" diye işaretlenir ve
/// yerine yenisi üretilir. <see cref="ReplacedByTokenHash"/> zinciri
/// kurar; böylece çalınmış bir jeton tespit edildiğinde o zincirin
/// tamamı iptal edilebilir.
/// </remarks>
public sealed class RefreshToken
{
    private RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    /// <summary>Jetonun hash'i. Jetonun kendisi hiç saklanmaz.</summary>
    public string TokenHash { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    /// <summary>Bu jetonun yerine üretilen jetonun hash'i (rotasyon zinciri).</summary>
    public string? ReplacedByTokenHash { get; private set; }

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        DateTimeOffset nowUtc,
        TimeSpan lifetime) =>
        new(Guid.NewGuid(), userId, tokenHash, nowUtc, nowUtc.Add(lifetime));

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsActive(DateTimeOffset nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    /// <summary>Jetonu iptal eder; isteğe bağlı olarak yerine geçeni kaydeder.</summary>
    public Result Revoke(DateTimeOffset nowUtc, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
        {
            return Result.Success();
        }

        RevokedAtUtc = nowUtc;
        ReplacedByTokenHash = replacedByTokenHash;

        return Result.Success();
    }
}
