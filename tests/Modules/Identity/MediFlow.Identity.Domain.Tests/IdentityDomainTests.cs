using MediFlow.BuildingBlocks.Common;

namespace MediFlow.Identity.Domain.Tests;

public class EmailTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Fail_When_Empty(string value)
    {
        Result<Email> result = Email.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.EmailEmpty", result.Error.Code);
    }

    [Theory]
    [InlineData("Ahmet@Mail.COM", "ahmet@mail.com")]
    [InlineData("  user@test.io  ", "user@test.io")]
    public void Create_Should_Normalise(string input, string expected)
    {
        // NORMALLEŞTİRME KRİTİK: aksi halde aynı e-posta ile iki hesap
        // açılabilir ve "zaten kayıtlı" kontrolü işe yaramaz.
        Result<Email> result = Email.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
    }

    [Fact]
    public void Create_Should_Fail_When_TooLong()
    {
        string tooLong = new('a', Email.MaxLength + 1);

        Result<Email> result = Email.Create($"{tooLong}@test.com");

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.EmailTooLong", result.Error.Code);
    }

    [Fact]
    public void Emails_Should_UseValueEquality()
    {
        Email a = Email.Create("same@test.com").Value;
        Email b = Email.Create("SAME@TEST.COM").Value;

        Assert.Equal(a, b);
    }
}

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private static Email ValidEmail() => Email.Create("user@test.com").Value;

    [Fact]
    public void Create_Should_StoreOnlyTheHash()
    {
        Result<User> result = User.Create(ValidEmail(), "hashed-value", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("hashed-value", result.Value.PasswordHash);
    }

    [Fact]
    public void Create_Should_AssignGivenRoles()
    {
        Result<User> result = User.Create(ValidEmail(), "hash", Now, roles: ["Patient", "Doctor"]);

        Assert.Equal(2, result.Value.Roles.Count);
    }

    [Fact]
    public void AssignRole_Should_Reject_Duplicates_IgnoringCase()
    {
        User user = User.Create(ValidEmail(), "hash", Now, roles: ["Patient"]).Value;

        Result second = user.AssignRole("patient");

        Assert.True(second.IsFailure);
        Assert.Equal("Identity.RoleAlreadyAssigned", second.Error.Code);
        Assert.Single(user.Roles);
    }

    [Fact]
    public void AssignRole_Should_Reject_Empty()
    {
        User user = User.Create(ValidEmail(), "hash", Now).Value;

        Result result = user.AssignRole("  ");

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.RoleEmpty", result.Error.Code);
    }

    [Fact]
    public void Roles_Should_NotBeModifiableFromOutside()
    {
        User user = User.Create(ValidEmail(), "hash", Now, roles: ["Patient"]).Value;

        Assert.IsNotType<List<string>>(user.Roles);
    }
}

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private static RefreshToken Issue() =>
        RefreshToken.Issue(Guid.NewGuid(), "token-hash", Now, Lifetime);

    [Fact]
    public void Issue_Should_BeActive()
    {
        RefreshToken token = Issue();

        Assert.True(token.IsActive(Now));
        Assert.False(token.IsRevoked);
        Assert.False(token.IsExpired(Now));
    }

    [Fact]
    public void Should_Expire_AfterLifetime()
    {
        RefreshToken token = Issue();

        Assert.False(token.IsExpired(Now.Add(Lifetime).AddSeconds(-1)));
        Assert.True(token.IsExpired(Now.Add(Lifetime)));
        Assert.False(token.IsActive(Now.Add(Lifetime)));
    }

    [Fact]
    public void Revoke_Should_MakeItInactive_AndRecordTheReplacement()
    {
        RefreshToken token = Issue();

        token.Revoke(Now.AddHours(1), "next-token-hash");

        Assert.True(token.IsRevoked);
        Assert.False(token.IsActive(Now.AddHours(1)));
        // Rotasyon zinciri: çalınmış jeton tespit edilince zincir izlenebilir.
        Assert.Equal("next-token-hash", token.ReplacedByTokenHash);
    }

    [Fact]
    public void Revoke_Should_BeIdempotent()
    {
        RefreshToken token = Issue();
        token.Revoke(Now.AddHours(1), "first");

        token.Revoke(Now.AddHours(2), "second");

        // İlk iptal zamanı ve zinciri korunuyor - ikinci çağrı üzerine yazmıyor.
        Assert.Equal("first", token.ReplacedByTokenHash);
        Assert.Equal(Now.AddHours(1), token.RevokedAtUtc);
    }
}
