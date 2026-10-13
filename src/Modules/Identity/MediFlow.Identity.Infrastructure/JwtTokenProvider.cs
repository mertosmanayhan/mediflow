using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MediFlow.BuildingBlocks.Common;
using MediFlow.Identity.Application.Abstractions;
using MediFlow.Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MediFlow.Identity.Infrastructure;

/// <summary>
/// JWT erişim jetonu ve yenileme jetonu üretir.
/// </summary>
/// <remarks>
/// JWT NEDİR, NE DEĞİLDİR?
///
/// Üç parçadan oluşur: header.payload.signature (nokta ile ayrık, Base64).
///
/// ⚠️ PAYLOAD ŞİFRELİ DEĞİL, SADECE İMZALI. Herkes Base64'ü çözüp
/// içeriğini okuyabilir - jwt.io'ya yapıştırmak yeterli. İmza, içeriğin
/// DEĞİŞTİRİLMEDİĞİNİ garanti eder, GİZLİ olduğunu garanti etmez.
///
/// SONUÇ: Jetona asla sır konmaz. Kullanıcı kimliği, rolleri, e-postası
/// olur; şifre, TC kimlik numarası, sağlık verisi OLMAZ.
///
/// İmza neyi sağlar: saldırgan payload'daki "role": "Patient" değerini
/// "Admin" yapsa imza geçersiz olur ve jeton reddedilir. Anahtar olmadan
/// geçerli imza üretemez.
///
/// NEDEN SUNUCUDA SAKLANMIYOR?
/// Ölçeklenebilirlik. 9 servis olduğunda her biri jetonu KENDİ BAŞINA
/// doğrulayabiliyor - ortak bir oturum veritabanına sormasına gerek yok.
/// Bedeli ise iptal edilemezlik; onu da kısa ömür + yenileme jetonu ile
/// dengeliyoruz.
/// </remarks>
internal sealed class JwtTokenProvider(IOptions<JwtOptions> options) : ITokenProvider
{
    private readonly JwtOptions _options = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public AccessToken CreateAccessToken(User user)
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            // 'sub' = subject: jetonun sahibi. Standart (RFC 7519) iddia adı.
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),

            // 'jti' = JWT ID: her jetona tekil kimlik. İleride tek tek
            // jeton iptali (deny list) gerekirse bu alan kullanılır.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // Roller ÇOĞUL: bir kullanıcının birden fazla rolü olabilir.
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // İZİNLER DE JETONA YAZILIYOR.
        //
        // Alternatif: her istekte kullanıcının rollerine bakıp izinleri
        // veritabanından çözmek. Jetona yazmak bir istek başına sorgu
        // tasarrufu demek - 9 servisli bir sistemde her servisin her
        // istekte yetki sorgusu yapması ciddi yük.
        //
        // BEDELİ: izinler jeton üretildiği ANDAKİ hali yansıtır. Yetki
        // geri alınırsa jeton süresi (15 dk) bitene kadar geçerli kalır.
        // Access token'ı kısa tutmamızın bir sebebi daha bu.
        //
        // Yetkinin ANINDA geri alınması gereken durumlar (hesap askıya
        // alma gibi) için ayrı bir mekanizma gerekir - ya daha kısa ömür
        // ya da merkezi bir iptal listesi. Her ikisi de ölçeklenebilirlik
        // ile güvenlik arasındaki aynı dengeyi başka noktada kuruyor.
        claims.AddRange(RolePermissions
            .For(user.Roles)
            .Select(permission => new Claim(Permissions.ClaimType, permission)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        string token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new AccessToken(token, expiresAt);
    }

    /// <summary>
    /// 256 bitlik kriptografik rastgele jeton üretir.
    /// </summary>
    /// <remarks>
    /// RandomNumberGenerator kullanılıyor, Random DEĞİL. Random
    /// öngörülebilir bir dizidir (tohumu bilinirse tüm çıktısı bilinir) ve
    /// kimlik bilgisi üretmek için kullanılması bir güvenlik açığıdır.
    /// Guid.NewGuid() de uygun değil: tekillik garantisi verir,
    /// tahmin edilemezlik garantisi vermez.
    /// </remarks>
    public string CreateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Yenileme jetonunun SHA-256 hash'i.
    /// </summary>
    /// <remarks>
    /// Hızlı hash burada DOĞRU tercih: girdi 256 bit kriptografik
    /// rastgele, yani kaba kuvvetle bulunamaz. Yavaşlatmanın faydası
    /// olmaz, zararı olur (her yenileme isteğinde hesaplanıyor).
    /// Şifrede ise tam tersi geçerli - bkz. BcryptPasswordHasher.
    /// </remarks>
    public string HashRefreshToken(string refreshToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
