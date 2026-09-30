using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Domain;

namespace MediFlow.PatientProfile.Application;

/// <summary>
/// "Yeni hasta kaydet" isteğinin taşıdığı veri.
/// </summary>
/// <remarks>
/// Neden ayrı bir tip? Metoda 3-4 parametre yerine tek bir nesne veriyoruz.
/// Alan eklendiğinde imza değişmiyor, ve ileride bu tip HTTP gövdesinden
/// doğrudan okunabilecek.
/// </remarks>
public sealed record CreatePatientRequest(string FirstName, string LastName, DateOnly BirthDate);

/// <summary>
/// "Hasta oluştur ve kaydet" iş akışı.
/// </summary>
/// <remarks>
/// Bu sınıf iki adımı SIRALAR, kural koymaz:
///   1. Domain'e "bu verilerle hasta oluştur" der  (kuralları Domain bilir)
///   2. Başarılıysa depoya kaydeder                 (nasıl kaydedildiğini bilmez)
///
/// İhtiyaç duyduğu iki şeyi DIŞARIDAN alıyor (parantez içindeki parametreler -
/// C#'ın "primary constructor" yazımı). Kendisi 'new' ile hiçbir şey
/// oluşturmuyor; bu sayede testte gerçek veritabanı ve gerçek saat yerine
/// sahteleri verilebiliyor.
/// </remarks>
public sealed class CreatePatientHandler(IPatientRepository patients, TimeProvider clock)
{
    /// <summary>
    /// İsteği işler. Başarılıysa yeni hastanın kimliğini döndürür.
    /// </summary>
    public async Task<Result<Guid>> HandleAsync(
        CreatePatientRequest request,
        CancellationToken cancellationToken = default)
    {
        // Saati BURADA okuyoruz, Domain'de değil. Domain'in saat okuması onu
        // test edilemez hale getirirdi; bu katman ise saati dışarıdan aldığı
        // için testte sabitlenebiliyor.
        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        Result<Patient> creation = Patient.Create(
            request.FirstName,
            request.LastName,
            request.BirthDate,
            today);

        if (creation.IsFailure)
        {
            // Hatayı OLDUĞU GİBİ yukarı taşıyoruz - yeni bir hata uydurmuyoruz.
            // Böylece "Patient.FirstNameEmpty" kodu ta HTTP cevabına kadar
            // bozulmadan gidebilecek.
            return Result.Failure<Guid>(creation.Error);
        }

        await patients.AddAsync(creation.Value, cancellationToken);

        return Result.Success(creation.Value.Id);
    }
}
