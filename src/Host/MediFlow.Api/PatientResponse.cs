using MediFlow.PatientProfile.Domain;

namespace MediFlow.Api;

/// <summary>
/// Hasta bilgisinin API'den dönen hali.
/// </summary>
/// <remarks>
/// NEDEN Patient'ı DOĞRUDAN JSON'A ÇEVİRMİYORUZ?
///
/// Çevirsek, sınıfın İÇ YAPISI API sözleşmesi olurdu. Sonuçları:
///   - Domain'de bir alanı yeniden adlandırmak, istemcileri kırar
///   - İstemciye göstermek istemediğimiz bir alan eklediğimizde sızar
///   - Domain, JSON'a nasıl göründüğünü düşünmeye başlar (katman ihlali)
///
/// Ayrı bir tip, iç yapıyı dış sözleşmeden AYIRIR. İkisi bağımsız evrilir.
/// Bu tipe genelde DTO (Data Transfer Object) denir.
/// </remarks>
public sealed record PatientResponse(
    Guid Id,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    IReadOnlyList<AllergyResponse> Allergies)
{
    public static PatientResponse From(Patient patient) =>
        new(
            patient.Id,
            patient.FirstName,
            patient.LastName,
            patient.BirthDate,
            [.. patient.Allergies.Select(AllergyResponse.From)]);
}

/// <summary>Bir alerjinin API'den dönen hali.</summary>
/// <remarks>
/// Severity'yi sayı değil METİN olarak veriyoruz. İstemci "3" görmek yerine
/// "High" görüyor; okunabilir ve enum'a sayı eklenirse anlam kaymaz.
/// </remarks>
public sealed record AllergyResponse(string Name, string Severity)
{
    public static AllergyResponse From(Allergy allergy) =>
        new(allergy.Name, allergy.Severity.ToString());
}
