using MediFlow.BuildingBlocks.Common;

namespace MediFlow.Identity.Domain;

public static class IdentityErrors
{
    public static readonly Error EmailEmpty =
        new("Identity.EmailEmpty", "Email is required.");

    public static readonly Error EmailTooLong =
        new("Identity.EmailTooLong", "Email is too long.");

    public static readonly Error EmailAlreadyRegistered =
        new("Identity.EmailAlreadyRegistered", "This email is already registered.");

    /// <summary>
    /// Giriş başarısız. TEK bir hata, bilerek.
    /// </summary>
    /// <remarks>
    /// GÜVENLİK: "kullanıcı bulunamadı" ile "şifre yanlış" AYRI hatalar
    /// olarak dönmez. Ayrı dönse saldırgan hangi e-postaların kayıtlı
    /// olduğunu tek tek deneyerek öğrenebilirdi (user enumeration).
    /// O liste, hedefli saldırı ve kimlik avı için değerlidir.
    ///
    /// Aynı sebeple giriş denemesinin SÜRESİ de ayırt edici olmamalı:
    /// kullanıcı yoksa hash doğrulaması hiç yapılmazsa cevap belirgin
    /// şekilde daha hızlı döner ve zamanlama üzerinden aynı bilgi sızar.
    /// Handler'da bu da ele alınıyor.
    /// </remarks>
    public static readonly Error InvalidCredentials =
        new("Identity.InvalidCredentials", "Email or password is incorrect.");

    public static readonly Error RoleEmpty =
        new("Identity.RoleEmpty", "Role name is required.");

    public static readonly Error RoleAlreadyAssigned =
        new("Identity.RoleAlreadyAssigned", "The user already has this role.");

    public static readonly Error RefreshTokenNotFound =
        new("Identity.RefreshTokenNotFound", "Refresh token is not valid.");

    public static readonly Error RefreshTokenExpired =
        new("Identity.RefreshTokenExpired", "Refresh token has expired.");

    /// <summary>
    /// Kullanılmış bir yenileme jetonu tekrar sunuldu.
    /// </summary>
    /// <remarks>
    /// Bu bir KULLANICI HATASI DEĞİL, bir SALDIRI SİNYALİDİR. Jeton
    /// rotasyonunda her yenileme eskisini geçersiz kılar; eski jetonun
    /// tekrar gelmesi, onun çalınmış olduğu anlamına gelir (meşru istemci
    /// elindekini çoktan değiştirmiştir). Bu durumda o kullanıcının tüm
    /// jeton zinciri iptal edilir.
    /// </remarks>
    public static readonly Error RefreshTokenReused =
        new("Identity.RefreshTokenReused", "Refresh token has already been used.");
}
