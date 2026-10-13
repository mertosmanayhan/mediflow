using MediatR;
using MediFlow.BuildingBlocks.Application;
using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Domain;

namespace MediFlow.PatientProfile.Application.Patients.DeletePatient;

public sealed record DeletePatientCommand(Guid PatientId) : ICommand<Guid>;

internal sealed class DeletePatientCommandHandler(IPatientRepository patients)
    : IRequestHandler<DeletePatientCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        DeletePatientCommand command,
        CancellationToken cancellationToken)
    {
        Patient? patient = await patients.GetByIdAsync(command.PatientId, cancellationToken);

        // Zaten silinmiş bir kayıt global query filter yüzünden null döner,
        // yani ikinci silme isteği 404 alır. Doğru davranış: istemci
        // açısından kayıt yok.
        if (patient is null)
        {
            return Result.Failure<Guid>(PatientErrors.NotFound);
        }

        patients.Remove(patient);

        // SaveChanges YOK - pipeline yazıyor.
        return Result.Success(patient.Id);
    }
}
