using FluentValidation;
using MediFlow.PatientProfile.Domain;

namespace MediFlow.PatientProfile.Application.Patients.CreatePatient;

/// <summary>
/// <see cref="CreatePatientCommand"/> için GİRDİ doğrulaması.
/// </summary>
/// <remarks>
/// BURADA NE VAR, DOMAIN'DE NE VAR?
///
/// Buradakiler isteğin ŞEKLİNE bakar - bağlam gerekmez, sadece gelen
/// veriye bakmak yeter:
///   - alan dolu mu
///   - uzunluk sınırı aşılmış mı
///
/// Domain'deki kurallar nesnenin DURUMUNU bilmek gerektirir:
///   - aynı alerji zaten kayıtlı mı
///   - doğum tarihi bugüne göre gelecekte mi
///
/// "Boş olamaz" kuralı KASITLI olarak iki yerde:
///   - Burada, çünkü kullanıcıya tüm eksikleri BİR SEFERDE söylemek
///     istiyoruz. Domain ilk hatada duruyor (önce ad, sonra soyad...).
///   - Domain'de, çünkü Patient.Create başka yollardan da çağrılabilir
///     (arka plan işi, veri aktarımı, test) ve o yollarda bu doğrulayıcı
///     çalışmaz. Domain SON savunma hattı, burası ERKEN uyarı.
///
/// Uzunluk sınırı ise SADECE burada - Domain'de yok. Sebebi: üst sınır
/// bir depolama/arayüz kararı, iş kuralı değil.
///
/// WithErrorCode: FluentValidation varsayılan olarak "NotEmptyValidator"
/// gibi teknik kodlar üretir. Bizim kodlarımızı veriyoruz ki istemci
/// Domain'in ürettiği kodlarla AYNI dili konuşsun.
/// </remarks>
public sealed class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
        RuleFor(command => command.FirstName)
            .NotEmpty().WithErrorCode("Patient.FirstNameEmpty")
            .MaximumLength(Patient.NameMaxLength).WithErrorCode("Patient.FirstNameTooLong");

        RuleFor(command => command.LastName)
            .NotEmpty().WithErrorCode("Patient.LastNameEmpty")
            .MaximumLength(Patient.NameMaxLength).WithErrorCode("Patient.LastNameTooLong");

        // default(DateOnly) = 0001-01-01. JSON'da alan hiç gönderilmezse
        // bu değer gelir; "doğum tarihi verilmedi" demenin yolu bu.
        RuleFor(command => command.BirthDate)
            .NotEqual(default(DateOnly)).WithErrorCode("Patient.BirthDateMissing");
    }
}
