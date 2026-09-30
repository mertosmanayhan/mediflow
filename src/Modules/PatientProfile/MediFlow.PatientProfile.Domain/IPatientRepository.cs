namespace MediFlow.PatientProfile.Domain;

/// <summary>
/// Hasta kayıtlarına erişim ihtiyacı. SADECE arayüz - gerçek kod başka yerde.
/// </summary>
/// <remarks>
/// NEDEN BU DOSYA DOMAIN İÇİNDE?
///
/// Çünkü ihtiyacı beyan eden taraf burası. Domain der: "bana hasta kaydedip
/// getirebilen bir şey lazım." Nasıl yapılacağını söylemez - SQL, dosya,
/// hafıza, hiç fark etmez.
///
/// Gerçek kodu (implementasyon) yazan taraf Infrastructure katmanı olacak ve
/// o katman bu arayüzü GÖRÜR, ama bu katman onu görmez. Yani ok şöyle döner:
///
///     Domain (arayüz)  ◄───  Infrastructure (gerçek kod)
///
/// Bu tersine çevrilmiş bağımlılık sayesinde veritabanını değiştirmek
/// domain'e hiç dokunmadan yapılabilir.
///
/// Şu an iki farklı dolduran var:
///   - Testlerde: hafızada tutan basit bir sınıf
///   - Sonra: EF Core ile MSSQL'e yazan sınıf
/// Domain ikisini de ayırt edemez, etmesi de gerekmez.
///
/// CancellationToken: uzun süren bir işlem iptal edilebilsin diye. Örneğin
/// kullanıcı sayfayı kapatırsa veritabanı sorgusunu boşuna bekletmeyiz.
/// </remarks>
public interface IPatientRepository
{
    /// <summary>Yeni bir hastayı kalıcı olarak kaydeder.</summary>
    Task AddAsync(Patient patient, CancellationToken cancellationToken = default);

    /// <summary>Kimliğe göre hasta getirir; yoksa <c>null</c>.</summary>
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
