using MediFlow.Identity.Domain;

namespace MediFlow.Identity.Application.Abstractions;

/// <summary>Üretilen erişim jetonu ve son geçerlilik anı.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Jeton üretimi ihtiyacı. Gerçekleştirimi Infrastructure'da (JWT).
/// </summary>
public interface ITokenProvider
{
    /// <summary>Kullanıcı için imzalı erişim jetonu üretir.</summary>
    AccessToken CreateAccessToken(User user);

    /// <summary>
    /// Kriptografik olarak güvenli, yüksek entropili rastgele jeton üretir.
    /// </summary>
    /// <remarks>
    /// Guid.NewGuid() KULLANILMAZ. Guid bir KİMLİK üreticisidir, sır
    /// üreticisi değil: tekilliği garanti eder ama tahmin edilemezliği
    /// garanti etmez. Kimlik bilgisi üretirken kriptografik rastgelelik
    /// (RandomNumberGenerator) gerekir.
    /// </remarks>
    string CreateRefreshToken();

    /// <summary>
    /// Yenileme jetonunun saklanacak hash'ini üretir.
    /// </summary>
    /// <remarks>
    /// NEDEN BURADA HIZLI HASH (SHA-256), ŞİFREDE YAVAŞ HASH (BCrypt)?
    ///
    /// Fark, girdinin ENTROPİSİNDE:
    ///   - Şifre: insan seçiyor, düşük entropi ("Ankara1234"). Saldırgan
    ///     sözlük/kaba kuvvet ile deneyebilir. Bu yüzden hash'in KASITLI
    ///     OLARAK YAVAŞ olması gerekir - her deneme pahalı olsun.
    ///   - Yenileme jetonu: 256 bit kriptografik rastgele. Deneme yoluyla
    ///     bulmak pratikte imkansız; yavaşlatmanın bir faydası yok.
    ///     Üstelik her yenileme isteğinde hash hesaplanacak, hızlı olması
    ///     İSTENİR.
    ///
    /// Bu ayrım mülakatta "neden şifreyi SHA256 ile hash'lemiyoruz?"
    /// sorusunun tam cevabıdır.
    /// </remarks>
    string HashRefreshToken(string refreshToken);

    /// <summary>Yenileme jetonunun ömrü.</summary>
    TimeSpan RefreshTokenLifetime { get; }
}
