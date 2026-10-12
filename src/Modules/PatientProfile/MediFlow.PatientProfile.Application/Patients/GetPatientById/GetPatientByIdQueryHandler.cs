using MediatR;
using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Domain;

namespace MediFlow.PatientProfile.Application.Patients.GetPatientById;

/// <summary>
/// <see cref="GetPatientByIdQuery"/> iş akışı.
/// </summary>
/// <remarks>
/// BU SINIF YENİ VE BİR KATMAN İHLALİNİ DÜZELTİYOR.
///
/// Önceki adımda GET endpoint'i doğrudan IPatientRepository'yi çağırıyordu:
///
///     app.MapGet("/patients/{id}", async (Guid id, IPatientRepository patients) => ...)
///
/// Çalışıyordu ama Api katmanı veri erişimine uzanıyordu. Sorunları:
///   - "Bulunamadı" kararı endpoint'in içindeydi, yani iş mantığı HTTP
///     koduna sızmıştı
///   - Okuma tarafı hiçbir behavior'dan geçmiyordu (loglanmıyordu)
///   - Yarın "sadece kendi hastasını görebilir" kuralı eklenince onu
///     endpoint'e yazmak gerekecekti
///
/// Artık okuma da yazma gibi pipeline'dan geçiyor ve Api katmanı sadece
/// HTTP işi yapıyor.
/// </remarks>
internal sealed class GetPatientByIdQueryHandler(IPatientRepository patients)
    : IRequestHandler<GetPatientByIdQuery, Result<PatientDto>>
{
    public async Task<Result<PatientDto>> Handle(
        GetPatientByIdQuery query,
        CancellationToken cancellationToken)
    {
        Patient? patient = await patients.GetByIdAsync(query.PatientId, cancellationToken);

        return patient is null
            ? Result.Failure<PatientDto>(PatientErrors.NotFound)
            : Result.Success(PatientDto.From(patient));
    }
}
