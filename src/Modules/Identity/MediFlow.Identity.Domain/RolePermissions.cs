using MediFlow.BuildingBlocks.Common;

namespace MediFlow.Identity.Domain;

/// <summary>
/// Rollerin hangi izinlere sahip olduğu - TEK doğruluk kaynağı.
/// </summary>
/// <remarks>
/// Endpoint'ler izin ister, roller izin taşır. Eşleme burada.
///
/// Bir rolün yetkisini değiştirmek = bu dosyada bir satır değiştirmek.
/// Hiçbir endpoint'e dokunmak gerekmez. Rol bazlı yaklaşımda aynı
/// değişiklik onlarca [Authorize(Roles = ...)] satırını taramak demekti.
///
/// SONRAKİ ADIM (bu projede yapılmadı, bilinçli): gerçek sistemlerde bu
/// eşleme veritabanında tutulur ve yönetici arayüzünden değiştirilebilir -
/// böylece yetki değişikliği dağıtım gerektirmez. Kodda tutmak, eşlemenin
/// sürüm kontrolünde ve gözden geçirilebilir olması avantajını verir.
/// Hangisinin doğru olduğu, yetkilerin ne sıklıkla değiştiğine bağlı.
/// </remarks>
public static class RolePermissions
{
    public const string Patient = "Patient";
    public const string Doctor = "Doctor";
    public const string Nurse = "Nurse";
    public const string Administrator = "Administrator";

    private static readonly Dictionary<string, string[]> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Hasta sadece kendi kaydını okuyabilir. "Sadece KENDİ kaydı"
        // kısıtı izinle değil kaynak bazlı yetkilendirmeyle çözülür
        // (resource-based authorization) - izin "okuyabilir" der, hangi
        // kaydı okuyabileceği ayrı bir kontroldür. Faz 2'de ele alınacak.
        [Patient] = [Permissions.PatientsRead],

        [Nurse] = [Permissions.PatientsRead, Permissions.PatientsWrite],

        [Doctor] = [Permissions.PatientsRead, Permissions.PatientsWrite],

        [Administrator] =
        [
            Permissions.PatientsRead,
            Permissions.PatientsWrite,
            Permissions.UsersRead,
            Permissions.UsersManageRoles,
        ],
    };

    /// <summary>
    /// Verilen rollerin birleşik izin kümesini döndürür.
    /// </summary>
    /// <remarks>
    /// Birden fazla rol varsa izinler BİRLEŞTİRİLİR (union), kesişim
    /// alınmaz. Yani roller yetki EKLER, kısıtlamaz. Tanınmayan rol
    /// sessizce yok sayılır - veritabanında eski bir rol adı kalmışsa
    /// uygulama çökmesin.
    /// </remarks>
    public static IReadOnlySet<string> For(IEnumerable<string> roles) =>
        roles
            .SelectMany(role => Map.TryGetValue(role, out string[]? permissions) ? permissions : [])
            .ToHashSet(StringComparer.Ordinal);
}
