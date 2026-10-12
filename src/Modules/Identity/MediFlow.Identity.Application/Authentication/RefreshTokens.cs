using FluentValidation;
using MediatR;
using MediFlow.BuildingBlocks.Application;
using MediFlow.BuildingBlocks.Common;
using MediFlow.Identity.Application.Abstractions;
using MediFlow.Identity.Domain;

namespace MediFlow.Identity.Application.Authentication;

public sealed record RefreshTokensCommand(string RefreshToken) : ICommand<AuthenticationResponse>;

public sealed class RefreshTokensCommandValidator : AbstractValidator<RefreshTokensCommand>
{
    public RefreshTokensCommandValidator() =>
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithErrorCode("Identity.RefreshTokenEmpty");
}

/// <summary>
/// Yenileme jetonunu kullanıp yeni bir jeton çifti üretir (rotasyon).
/// </summary>
/// <remarks>
/// AKIŞ:
///   1. Gelen jetonun hash'ini hesapla, kaydı bul
///   2. Kayıt yok -> geçersiz
///   3. Kayıt İPTAL EDİLMİŞ -> YENİDEN KULLANIM: saldırı sinyali,
///      kullanıcının tüm jetonlarını iptal et
///   4. Süresi dolmuş -> geçersiz
///   5. Geçerli -> eskisini iptal et, yenisini üret, zinciri bağla
///
/// 3. ADIM BU İŞİN KALBİ. Rotasyon olmadan çalınmış bir jeton, ömrü
/// boyunca sessizce kullanılabilir. Rotasyonla birlikte: meşru istemci
/// jetonu yeniledikten sonra saldırgan eskisini kullanmaya çalışırsa
/// (veya tersi) sistem bunu FARK EDER - çünkü aynı jeton iki kez
/// sunulmuş olur. Hangi tarafın saldırgan olduğunu bilemediğimiz için
/// güvenli davranış her şeyi iptal etmek; kullanıcı yeniden giriş yapar.
/// </remarks>
internal sealed class RefreshTokensCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenProvider tokenProvider,
    TimeProvider clock) : IRequestHandler<RefreshTokensCommand, Result<AuthenticationResponse>>
{
    public async Task<Result<AuthenticationResponse>> Handle(
        RefreshTokensCommand command,
        CancellationToken cancellationToken)
    {
        DateTimeOffset nowUtc = clock.GetUtcNow();

        string incomingHash = tokenProvider.HashRefreshToken(command.RefreshToken);

        RefreshToken? existing = await refreshTokens.GetByHashAsync(incomingHash, cancellationToken);

        if (existing is null)
        {
            return Result.Failure<AuthenticationResponse>(IdentityErrors.RefreshTokenNotFound);
        }

        // YENİDEN KULLANIM TESPİTİ
        if (existing.IsRevoked)
        {
            await refreshTokens.RevokeAllForUserAsync(existing.UserId, nowUtc, cancellationToken);
            await refreshTokens.SaveChangesAsync(cancellationToken);

            return Result.Failure<AuthenticationResponse>(IdentityErrors.RefreshTokenReused);
        }

        if (existing.IsExpired(nowUtc))
        {
            return Result.Failure<AuthenticationResponse>(IdentityErrors.RefreshTokenExpired);
        }

        User? user = await users.GetByIdAsync(existing.UserId, cancellationToken);

        if (user is null)
        {
            // Jeton geçerli ama kullanıcı silinmiş. Jetonu da kapat.
            existing.Revoke(nowUtc);
            await refreshTokens.SaveChangesAsync(cancellationToken);

            return Result.Failure<AuthenticationResponse>(IdentityErrors.RefreshTokenNotFound);
        }

        // ROTASYON: yeni jeton üret, eskisini yenisine işaret ederek iptal et.
        string newRefreshToken = tokenProvider.CreateRefreshToken();
        string newRefreshTokenHash = tokenProvider.HashRefreshToken(newRefreshToken);

        existing.Revoke(nowUtc, newRefreshTokenHash);

        RefreshToken replacement = RefreshToken.Issue(
            user.Id,
            newRefreshTokenHash,
            nowUtc,
            tokenProvider.RefreshTokenLifetime);

        await refreshTokens.AddAsync(replacement, cancellationToken);
        await refreshTokens.SaveChangesAsync(cancellationToken);

        AccessToken accessToken = tokenProvider.CreateAccessToken(user);

        return Result.Success(new AuthenticationResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            newRefreshToken));
    }
}
