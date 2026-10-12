using MediatR;
using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Domain;

namespace MediFlow.PatientProfile.Application.Patients.CreatePatient;

/// <summary>
/// <see cref="CreatePatientCommand"/> iş akışı.
/// </summary>
/// <remarks>
/// Bu sınıf ESKİDEN de vardı (CreatePatientHandler) ve içeriği neredeyse
/// aynı. Değişen tek şey: artık MediatR'ın <c>IRequestHandler</c>
/// arayüzünü uyguluyor.
///
/// Kazanç nerede? Endpoint artık bu sınıfın ADINI BİLMİYOR. Sadece
/// "şu komutu gönder" diyor. Sonuç:
///   - Endpoint ile handler arasındaki derleme zamanı bağı koptu
///   - Araya behavior zinciri girebiliyor (doğrulama, loglama...)
///   - Handler'ı bölmek/yeniden adlandırmak endpoint'i etkilemiyor
///
/// 'internal' çünkü dışarıdan kimsenin bu sınıfı doğrudan çağırmaması
/// gerekiyor - komut gönderilir, handler çağrılmaz. Kapsülleme.
/// </remarks>
internal sealed class CreatePatientCommandHandler(
    IPatientRepository patients,
    TimeProvider clock) : IRequestHandler<CreatePatientCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePatientCommand command,
        CancellationToken cancellationToken)
    {
        // Saati bu katman okuyor; Domain okumuyor (test edilebilirlik).
        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        Result<Patient> creation = Patient.Create(
            command.FirstName,
            command.LastName,
            command.BirthDate,
            today);

        if (creation.IsFailure)
        {
            // Domain'in hatasını OLDUĞU GİBİ taşıyoruz, yenisini uydurmuyoruz.
            return Result.Failure<Guid>(creation.Error);
        }

        await patients.AddAsync(creation.Value, cancellationToken);

        return Result.Success(creation.Value.Id);
    }
}
