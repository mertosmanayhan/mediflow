namespace MediFlow.BuildingBlocks.Common;

/// <summary>
/// Sistemdeki tüm izinlerin merkezi kataloğu.
/// </summary>
/// <remarks>
/// NEDEN ROL DEĞİL İZİN?
///
/// Rol bazlı yetkilendirmede kod şöyle olur:
///     [Authorize(Roles = "Doctor,Nurse,Admin")]
///
/// Sorunları:
///   1. Yeni bir rol eklendiğinde (ör. "Stajyer Doktor") KODA dokunmak ve
///      yeniden dağıtmak gerekir. Oysa rol tanımı bir İŞ kararıdır, kod
///      kararı değil.
///   2. Aynı listeyi onlarca endpoint'te tekrarlamak gerekir; biri
///      güncellenmeyi unutulur.
///   3. Kod "kim" sorusuna cevap verir, "ne yapabilir" sorusuna vermez.
///      Oysa endpoint'in ilgilendiği şey yetkidir, kimlik değil.
///
/// İzin bazlıda kod şöyle olur:
///     .RequirePermission(Permissions.PatientsWrite)
///
/// Endpoint "hasta yazma yetkisi" istiyor. Hangi rollerin bu yetkiye
/// sahip olduğu AYRI bir yerde tanımlı (RolePermissions) ve değişmesi
/// endpoint'i etkilemiyor.
///
/// Bu, "indirection" ekleyerek değişim maliyetini düşürmenin tipik bir
/// örneği: rol -> izin eşlemesi tek yerde, izin -> endpoint eşlemesi
/// kodda, ve ikisi birbirinden bağımsız evriliyor.
///
/// ADLANDIRMA: "kaynak:eylem" biçimi. Okunur, gruplanabilir ve
/// ileride joker eşleme (patients:*) yapılabilir.
/// </remarks>
public static class Permissions
{
    /// <summary>JWT içindeki izin iddialarının tipi.</summary>
    public const string ClaimType = "permission";

    // --- Hasta profili ---
    public const string PatientsRead = "patients:read";
    public const string PatientsWrite = "patients:write";

    // --- Kullanıcı yönetimi (Faz ilerisi) ---
    public const string UsersRead = "users:read";
    public const string UsersManageRoles = "users:manage-roles";
}
