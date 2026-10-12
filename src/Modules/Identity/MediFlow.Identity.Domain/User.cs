using MediFlow.BuildingBlocks.Common;

namespace MediFlow.Identity.Domain;

/// <summary>
/// Sisteme giriş yapabilen kullanıcı.
/// </summary>
/// <remarks>
/// DİKKAT: Burada "password" diye bir alan YOK, sadece
/// <see cref="PasswordHash"/> var. Düz metin şifre bu sınıfa hiç girmiyor;
/// hash'leme Application katmanında, algoritma Infrastructure'da.
/// Böylece şifrenin kendisi hiçbir zaman bellekte uzun süre tutulmuyor ve
/// kazara loglanma yüzeyi daralıyor.
/// </remarks>
public sealed class User
{
    private readonly List<string> _roles = [];

    private User(Guid id, Email email, string passwordHash, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public Email Email { get; private set; }

    /// <summary>Hash'lenmiş şifre. Düz metin ASLA saklanmaz.</summary>
    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Roller - salt okunur görünüm.</summary>
    public IReadOnlyList<string> Roles => _roles.AsReadOnly();

    public static Result<User> Create(
        Email email,
        string passwordHash,
        DateTimeOffset nowUtc,
        IEnumerable<string>? roles = null)
    {
        var user = new User(Guid.NewGuid(), email, passwordHash, nowUtc);

        foreach (string role in roles ?? [])
        {
            Result assignment = user.AssignRole(role);

            if (assignment.IsFailure)
            {
                return Result.Failure<User>(assignment.Error);
            }
        }

        return Result.Success(user);
    }

    public Result AssignRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Result.Failure(IdentityErrors.RoleEmpty);
        }

        string normalized = role.Trim();

        if (_roles.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(IdentityErrors.RoleAlreadyAssigned);
        }

        _roles.Add(normalized);

        return Result.Success();
    }

    /// <summary>
    /// Şifre değiştirir. Yeni hash'i dışarıdan alır.
    /// </summary>
    public void ChangePasswordHash(string newPasswordHash) =>
        PasswordHash = newPasswordHash;
}
