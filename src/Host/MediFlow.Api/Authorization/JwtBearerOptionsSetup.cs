using System.Text;
using MediFlow.Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MediFlow.Api.Authorization;

/// <summary>
/// Gelen JWT'nin nasıl doğrulanacağını yapılandırır.
/// </summary>
/// <remarks>
/// NEDEN AYRI BİR SINIF VE NEDEN IOptions?
///
/// Önce bu ayarlar Program.cs'te satır içinde yapılıyordu ve anahtar
/// doğrudan okunuyordu:
///
///     var jwt = builder.Configuration.GetSection("Jwt").Get&lt;JwtOptions&gt;();
///
/// Bu HEMEN (eager) bir okumadır: yapılandırma o anda ne ise o alınır.
/// Jeton ÜRETİMİ ise IOptions&lt;JwtOptions&gt; ile TEMBEL (lazy) okuyordu -
/// yani değer ilk kullanıldığında çözülüyordu.
///
/// İkisi normalde aynı sonucu verir. Ama integration testinde fabrika
/// yapılandırmayı sonradan eziyor:
///   - üretim: test anahtarını gördü  (tembel)
///   - doğrulama: gerçek anahtarı gördü (hemen)
/// Sonuç: her kimlik doğrulaması 401. Belirtiyle sebep arasında hiçbir
/// görünür bağ yoktu.
///
/// DERS: yapılandırmayı hemen okumak, "o an ne varsa onu dondurur".
/// IOptions ile okumak aynı değeri tüm bileşenlerin görmesini garanti
/// eder. Bu hatayı bulan şey integration testiydi - birim testler
/// bileşenleri ayrı ayrı doğruladığı için ikisinin FARKLI değer gördüğünü
/// hiç fark edemezdi. Entegrasyon testinin varlık sebebi tam olarak bu.
/// </remarks>
internal sealed class JwtBearerOptionsSetup(IOptions<JwtOptions> jwtOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public void Configure(string? name, JwtBearerOptions options) => Configure(options);

    // HER DOĞRULAMA AÇIK, bilerek. Kapatılan her kontrol bir açıktır.
    public void Configure(JwtBearerOptions options) =>
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Jetonu BİZ mi ürettik? Başka servisin jetonu kabul edilmesin.
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,

            // Jeton BİZE mi verilmiş? Başka API için üretilmiş jeton
            // burada geçmesin.
            ValidateAudience = true,
            ValidAudience = _jwt.Audience,

            // İmza geçerli mi? Kapatılırsa herkes kendi jetonunu yazıp
            // Admin olabilir.
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwt.SigningKey)),

            ValidateLifetime = true,

            // VARSAYILAN 5 DAKİKA - yani 15 dakikalık jeton fiilen 20
            // dakika yaşar. Saat kaymasına küçük tolerans yeterli.
            ClockSkew = TimeSpan.FromSeconds(30),
        };
}
