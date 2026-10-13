using FluentValidation;
using MediatR;
using MediFlow.BuildingBlocks.Application;
using MediFlow.BuildingBlocks.Common;
using MediFlow.Identity.Application.Abstractions;
using MediFlow.Identity.Domain;

namespace MediFlow.Identity.Application.Authentication;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthenticationResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Burada şifre uzunluğu KONTROL EDİLMİYOR, bilerek.
        // Girişte "şifre en az 12 karakter olmalı" demek, saldırgana
        // "bu hesabın şifresi 12+ karakter" bilgisini verir ve eski
        // kurallarla açılmış hesapları kilitler. Girişte tek soru:
        // "bu kimlik bilgileri doğru mu?"
        RuleFor(command => command.Email).NotEmpty().WithErrorCode("Identity.EmailEmpty");
        RuleFor(command => command.Password).NotEmpty().WithErrorCode("Identity.PasswordEmpty");
    }
}

internal sealed class LoginCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    TimeProvider clock) : IRequestHandler<LoginCommand, Result<AuthenticationResponse>>
{
    /// <summary>
    /// Var olmayan kullanıcı için doğrulama süresini dengelemek üzere
    /// kullanılan sahte hash.
    /// </summary>
    /// <remarks>
    /// ZAMANLAMA SALDIRISINA KARŞI (timing attack).
    ///
    /// Kullanıcı yoksa hemen dönsek, cevap belirgin şekilde daha hızlı
    /// gelirdi (BCrypt doğrulaması ~100 ms sürüyor). Saldırgan sadece
    /// süreye bakarak hangi e-postaların kayıtlı olduğunu anlayabilirdi -
    /// yani hata mesajını tek tip yapma çabamız boşa giderdi.
    ///
    /// Çözüm: kullanıcı yoksa da bir hash doğrulaması çalıştırmak. Sonucu
    /// kullanılmıyor; amacı yalnızca geçen süreyi benzer kılmak.
    /// </remarks>
    private const string DummyHashForTimingEqualisation =
        "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";

    public async Task<Result<AuthenticationResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(command.Email);

        if (email.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidCredentials);
        }

        User? user = await users.GetByEmailAsync(email.Value, cancellationToken);

        if (user is null)
        {
            passwordHasher.Verify(command.Password, DummyHashForTimingEqualisation);

            // Hata mesajı "kullanıcı yok" DEĞİL. Bkz. IdentityErrors.InvalidCredentials
            return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidCredentials);
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            // Şifre yanlış - ama AYNI hata dönüyor.
            return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidCredentials);
        }

        return Result.Success(
            await IssueTokensAsync(user, cancellationToken));
    }

    private async Task<AuthenticationResponse> IssueTokensAsync(
        User user,
        CancellationToken cancellationToken)
    {
        AccessToken accessToken = tokenProvider.CreateAccessToken(user);

        string refreshToken = tokenProvider.CreateRefreshToken();
        string refreshTokenHash = tokenProvider.HashRefreshToken(refreshToken);

        RefreshToken record = RefreshToken.Issue(
            user.Id,
            refreshTokenHash,
            clock.GetUtcNow(),
            tokenProvider.RefreshTokenLifetime);

        await refreshTokens.AddAsync(record, cancellationToken);

        // Dışarıya jetonun KENDİSİ dönüyor; veritabanında HASH'i duruyor.
        // Sunucu bu jetonu bir daha asla göremez - tıpkı şifre gibi.
        return new AuthenticationResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken);
    }
}
