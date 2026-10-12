using MediFlow.Identity.Domain;

namespace MediFlow.Identity.Infrastructure;

/// <summary>
/// BCrypt ile şifre hash'leme.
/// </summary>
/// <remarks>
/// NEDEN BCrypt, NEDEN SHA-256 DEĞİL?
///
/// 1. KASITLI YAVAŞLIK. SHA-256 saniyede milyarlarca hash hesaplar; bu
///    bir şifre hash'i için felakettir çünkü saldırgan da aynı hızla
///    deneyebilir. BCrypt'in iş faktörü (work factor) her artırımda
///    süreyi iki katına çıkarır. 12 faktörü ~250 ms demek - kullanıcı
///    için fark etmez, saldırgan için kaba kuvveti imkansızlaştırır.
///
/// 2. TUZ (salt) OTOMATİK. BCrypt her hash'e rastgele bir tuz ekler ve
///    onu hash dizgisinin içinde saklar. Sonuç: aynı şifreye sahip iki
///    kullanıcının hash'i FARKLI olur. Tuz olmazsa önceden hesaplanmış
///    tablolarla (rainbow table) toplu kırma mümkün olurdu.
///
/// 3. AYARLANABİLİR. Donanım hızlandıkça iş faktörü artırılır; eski
///    hash'ler çalışmaya devam eder çünkü faktör hash'in içinde yazılı.
///
/// Alternatifler: Argon2 (daha modern, belleğe de dayanıklı - GPU
/// saldırılarına karşı daha iyi) ve PBKDF2 (ASP.NET Core Identity'nin
/// varsayılanı). BCrypt olgun, yaygın ve bu ölçek için yeterli.
///
/// WORK FACTOR SEÇİMİ: 12. Daha yüksek değer daha güvenli ama her giriş
/// isteğini yavaşlatır - yani bir hizmet reddi (DoS) yüzeyi de yaratır.
/// 12, güncel donanımda ~250 ms ile yaygın kabul gören denge.
/// </remarks>
internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Bozuk/eksik hash verisi. "Doğrulanamadı" olarak ele alıyoruz;
            // istisnayı yukarı taşımak giriş akışını 500'e çevirirdi ve
            // saldırgana veri bozulması hakkında ipucu verirdi.
            return false;
        }
    }
}
