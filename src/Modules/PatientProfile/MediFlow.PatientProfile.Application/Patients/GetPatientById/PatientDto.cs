using MediFlow.PatientProfile.Domain;

namespace MediFlow.PatientProfile.Application.Patients.GetPatientById;

/// <summary>
/// Sorgunun döndürdüğü hasta görünümü.
/// </summary>
/// <remarks>
/// NEDEN DOMAIN NESNESİ DEĞİL?
/// <see cref="Patient"/>'ı doğrudan döndürsek iç yapısı dış sözleşme
/// olurdu: bir alanı yeniden adlandırmak istemcileri kırar, göstermek
/// istemediğimiz bir alan eklediğimizde sızar.
///
/// Bu tip Api katmanında DEĞİL Application'da duruyor. Sebebi: sorgunun
/// sonucu, sorgunun bir parçasıdır. Api katmanında ayrı bir tip daha
/// yapıp iki kez eşleme yapmak bu ölçekte gereksiz tekrar olur.
///
/// Bedeli: Application'ın tipi fiilen HTTP sözleşmesi haline geliyor.
/// Kabul ediyoruz, çünkü tek tüketici kendi API'miz. İki farklı istemci
/// farklı şekiller isterse (ör. mobil daha az alan) o zaman Api
/// katmanında ayrı tipler açılır.
/// </remarks>
public sealed record PatientDto(
    Guid Id,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    IReadOnlyList<AllergyDto> Allergies)
{
    internal static PatientDto From(Patient patient) =>
        new(
            patient.Id,
            patient.FirstName,
            patient.LastName,
            patient.BirthDate,
            [.. patient.Allergies.Select(AllergyDto.From)]);
}

/// <summary>Bir alerjinin dışarıya dönen hali.</summary>
/// <remarks>
/// Severity sayı değil METİN olarak gidiyor: istemci "3" yerine "High"
/// görüyor. Veritabanında sayı olarak saklanıyor (sıralanabilmesi için),
/// dışarıya metin olarak çıkıyor (okunabilmesi için). İki ayrı ihtiyaç,
/// iki ayrı temsil - DTO'nun varlık sebebi tam bu.
/// </remarks>
public sealed record AllergyDto(string Name, string Severity)
{
    internal static AllergyDto From(Allergy allergy) =>
        new(allergy.Name, allergy.Severity.ToString());
}
