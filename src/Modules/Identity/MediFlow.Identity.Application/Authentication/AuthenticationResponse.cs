namespace MediFlow.Identity.Application.Authentication;

/// <summary>
/// Giriş ve yenileme işlemlerinin döndürdüğü jeton çifti.
/// </summary>
/// <remarks>
/// İstemci erişim jetonunu her istekte "Authorization: Bearer ..." olarak
/// gönderir. Süresi dolunca yenileme jetonuyla yeni bir çift alır -
/// kullanıcıya şifresini tekrar sormadan.
///
/// NOT: Yenileme jetonu gövdede dönüyor. Üretimde tarayıcı istemcileri için
/// daha güvenli yol HttpOnly + Secure + SameSite çerez kullanmaktır; o zaman
/// JavaScript jetona hiç erişemez ve XSS ile çalınamaz. Faz 9'da React
/// arayüzünü yazarken bu kararı ele alacağız.
/// </remarks>
public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken);
