using MediFlow.BuildingBlocks.Common;

namespace MediFlow.PatientProfile.Domain;

/// <summary>
/// A patient. Owns its own rules: an invalid patient cannot be constructed,
/// and its allergy list cannot be corrupted from outside.
/// </summary>
public sealed class Patient
{
    // GERÇEK liste burada ve PRIVATE. Dışarıdan erişilemez.
    private readonly List<Allergy> _allergies = [];

    // Kurucu PRIVATE: dışarıdan 'new Patient(...)' yazılamaz.
    // Tek giriş kapısı aşağıdaki Create metodu -> kural atlanamaz.
    private Patient(Guid id, string firstName, string lastName, DateOnly birthDate)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        BirthDate = birthDate;
    }

    /// <summary>Kimlik. İki hastanın adı aynı olabilir; onları bu ayırır.</summary>
    public Guid Id { get; }

    // 'private set': dışarıdan patient.FirstName = "x" YAZILAMAZ.
    // Değişiklik gerekirse kuralı içeren bir metot eklenir.
    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    /// <summary>
    /// Doğum tarihi. <c>DateOnly</c> kullanıyoruz çünkü doğum tarihinin saati yok;
    /// <c>DateTime</c> kullanmak anlamsız bir "00:00:00" taşımak olurdu.
    /// </summary>
    public DateOnly BirthDate { get; private set; }

    /// <summary>
    /// Alerjiler - SALT OKUNUR görünüm.
    /// </summary>
    /// <remarks>
    /// <c>AsReadOnly()</c> şart. Sadece dönüş tipini <c>IReadOnlyList</c> yapmak
    /// yetmez: içerideki <c>List</c>'i olduğu gibi döndürürsek çağıran onu
    /// <c>List&lt;Allergy&gt;</c>'ye geri çevirip <c>Add</c> çağırabilir ve
    /// kurallarımızı tamamen atlar. <c>AsReadOnly()</c> o kaçağı kapatır.
    ///
    /// Kopya değil, canlı bir sarmalayıcı döndürür - yani liste değişince
    /// bu görünüm de güncel kalır, ama üzerinden değişiklik yapılamaz.
    /// </remarks>
    public IReadOnlyList<Allergy> Allergies => _allergies.AsReadOnly();

    /// <summary>
    /// Yeni hasta oluşturur. Kurallara uymazsa nesne HİÇ oluşmaz.
    /// </summary>
    /// <param name="firstName">Ad. Boş olamaz; baştaki/sondaki boşluklar kırpılır.</param>
    /// <param name="lastName">Soyad. Boş olamaz; baştaki/sondaki boşluklar kırpılır.</param>
    /// <param name="birthDate">Doğum tarihi. Bugünden sonra olamaz.</param>
    /// <param name="today">
    /// Bugünün tarihi, DIŞARIDAN verilir. Sınıf içinde <c>DateTime.Now</c>
    /// okumuyoruz çünkü o zaman testte "yarın"ı üretemezdik ve testler
    /// gerçek takvime bağlı, kırılgan hale gelirdi.
    /// </param>
    /// <returns>
    /// Başarılıysa yeni hasta; değilse <see cref="PatientErrors"/> içindeki
    /// ilgili hata.
    /// </returns>
    public static Result<Patient> Create(
        string firstName,
        string lastName,
        DateOnly birthDate,
        DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return Result.Failure<Patient>(PatientErrors.FirstNameEmpty);
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return Result.Failure<Patient>(PatientErrors.LastNameEmpty);
        }

        // '>' kullanıyoruz, '>=' değil: bugün doğan bebek geçerli bir hastadır.
        if (birthDate > today)
        {
            return Result.Failure<Patient>(PatientErrors.BirthDateInFuture);
        }

        var patient = new Patient(
            Guid.NewGuid(),
            firstName.Trim(),
            lastName.Trim(),
            birthDate);

        return Result.Success(patient);
    }

    /// <summary>
    /// Hastanın kaydına bir alerji ekler.
    /// </summary>
    /// <param name="name">Alerjen adı. Boş olamaz; boşluklar kırpılır.</param>
    /// <param name="severity">Reaksiyonun şiddeti.</param>
    /// <returns>
    /// Aynı alerji zaten kayıtlıysa veya ad boşsa başarısız sonuç.
    /// Başarısız durumda liste DEĞİŞMEZ.
    /// </returns>
    public Result AddAllergy(string name, AllergySeverity severity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(PatientErrors.AllergyNameEmpty);
        }

        string normalized = name.Trim();

        // Büyük/küçük harf farkı aynı alerjiyi iki kez kaydetmeye yol açmasın:
        // "Penisilin" ve "PENISILIN" aynı maddedir.
        //
        // NOT: OrdinalIgnoreCase Türkçe'nin i/İ ve I/ı kurallarını uygulamaz.
        // Latin harfli alerjen adları için sorun değil, ama Türkçe metin
        // karşılaştırması genel bir konu olarak açık bir karar bekliyor
        // (bkz. journal: collation kararı).
        bool alreadyExists = _allergies.Any(allergy =>
            string.Equals(allergy.Name, normalized, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            return Result.Failure(PatientErrors.AllergyAlreadyExists);
        }

        _allergies.Add(new Allergy(normalized, severity));

        return Result.Success();
    }
}
