namespace MediFlow.Identity.Infrastructure;

/// <summary>
/// JWT yapılandırması. appsettings + kullanıcı sırlarından bağlanır.
/// </summary>
/// <remarks>
/// SigningKey bir SIRDIR ve appsettings.json'a YAZILMAZ - user-secrets
/// (yerel) veya AWS Secrets Manager (Faz 7) üzerinden gelir.
///
/// Anahtar sızarsa saldırgan İSTEDİĞİ kullanıcı için geçerli jeton
/// üretebilir; şifreleri bilmesi gerekmez. Yani bu anahtar, veritabanı
/// şifresinden daha kritiktir.
/// </remarks>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 için gereken en az anahtar uzunluğu (bayt).</summary>
    public const int MinimumSigningKeyBytes = 32;

    // NOT - DENEYEREK ÖĞRENİLDİ: Bu özellikler 'init' değil 'set'.
    // services.Configure<T>(section) var olan bir nesneye Bind yapıyor ve
    // init-only özellikleri atayamıyor. Sonuç SESSİZDİ: Issuer ve Audience
    // boş kaldı, jeton bu iddialar olmadan üretildi, doğrulama onu reddetti.
    // Belirti 401'di; sebebi yapılandırma bağlamasıydı - ilişkisi hiç
    // belli değil. IdentityModule'daki ValidateOnStart() artık bu sınıf
    // hatayı başlangıçta patlatıyor.

    /// <summary>Jetonu kim üretti (bizim servis).</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Jeton kim için üretildi (bizim API).</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// İmzalama anahtarı. HMAC-SHA256 için en az 256 bit (32 bayt) olmalı.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Erişim jetonu ömrü. KISA olmalı.
    /// </summary>
    /// <remarks>
    /// JWT sunucuda saklanmaz, imzası doğrulanıp kabul edilir - bu yüzden
    /// GERİ ALINAMAZ. Kullanıcı çıkış yapsa, hesabı kapatılsa, rolü
    /// değişse bile jeton süresi bitene kadar geçerlidir.
    ///
    /// Tek savunma ömrü kısa tutmak: çalınan jetonun işe yarama penceresi
    /// 15 dakika. Uzun ömürlü erişim jetonu, iptal edilemeyen bir
    /// kalıcı şifre gibidir.
    /// </remarks>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Yenileme jetonu ömrü (gün).</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
