using MediFlow.BuildingBlocks.Common;

namespace MediFlow.PatientProfile.Domain;

/// <summary>
/// A patient. Owns its own rules: an invalid patient cannot be constructed.
/// </summary>
public sealed class Patient
{
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
}
