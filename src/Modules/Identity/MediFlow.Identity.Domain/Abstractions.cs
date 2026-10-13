namespace MediFlow.Identity.Domain;

/// <summary>Kullanıcı kayıtlarına erişim ihtiyacı.</summary>
public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken = default);
}

/// <summary>Yenileme jetonu kayıtlarına erişim ihtiyacı.</summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir kullanıcının TÜM aktif jetonlarını iptal eder.
    /// </summary>
    /// <remarks>
    /// Jeton yeniden kullanımı tespit edildiğinde çağrılır: elimizde
    /// çalınmış bir jeton olduğunu biliyoruz ama hangisinin saldırganda
    /// hangisinin kullanıcıda olduğunu bilmiyoruz. Güvenli davranış
    /// hepsini iptal etmek - kullanıcı yeniden giriş yapar, saldırgan
    /// dışarıda kalır.
    /// </remarks>
    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

/// <summary>
/// Şifre hash'leme ihtiyacı. Algoritma Infrastructure'da seçilir.
/// </summary>
/// <remarks>
/// Domain algoritmayı bilmiyor; bu kasıtlı. Hash algoritmaları zamanla
/// kırılır veya yetersiz hale gelir (donanım hızlandıkça). Algoritmayı
/// değiştirmek Domain'e dokunmadan yapılabilmeli.
/// </remarks>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
