using MediatR;
using MediFlow.Api.Authorization;
using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Application.Patients.CreatePatient;
using MediFlow.PatientProfile.Application.Patients.DeletePatient;
using MediFlow.PatientProfile.Application.Patients.GetPatientById;

namespace MediFlow.Api.Endpoints;

internal static class PatientEndpoints
{
    internal static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        // Grup seviyesinde kimlik doğrulama: yeni endpoint ekleyip korumayı
        // unutmak mümkün değil. Güvenlikte varsayılan KAPALI olmalı.
        RouteGroupBuilder group = app.MapGroup("/patients")
            .WithTags("Patients")
            .RequireAuthorization();

        // Endpoint İZİN istiyor, rol değil (aşağıdaki RequirePermission).
        // Hangi rollerin bu izne sahip olduğu RolePermissions'da tutuluyor
        // ve burayı hiç etkilemez: yeni bir rol eklemek endpoint'e
        // dokunmayı gerektirmiyor.
        group.MapPost("/", async (
                CreatePatientCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result<Guid> result = await sender.Send(command, cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"/patients/{result.Value}", new { id = result.Value })
                    : result.Error.ToHttpProblem();
            })
            .WithName("CreatePatient")
            .WithSummary("Yeni hasta kaydı oluşturur")
            .RequirePermission(Permissions.PatientsWrite);

        group.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result<PatientDto> result = await sender.Send(
                    new GetPatientByIdQuery(id), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.Error.ToHttpProblem();
            })
            .WithName("GetPatientById")
            .WithSummary("Kimliğe göre hasta getirir")
            .RequirePermission(Permissions.PatientsRead);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result<Guid> result = await sender.Send(
                    new DeletePatientCommand(id), cancellationToken);

                // 204 No Content: işlem başarılı, dönecek gövde yok.
                // 200 + boş gövde yerine 204 kullanmak REST'te doğru olanı:
                // istemciye "cevabı ayrıştırmaya çalışma" der.
                return result.IsSuccess
                    ? Results.NoContent()
                    : result.Error.ToHttpProblem();
            })
            .WithName("DeletePatient")
            .WithSummary("Hasta kaydını siler (soft delete)")
            .RequirePermission(Permissions.PatientsWrite);
    }
}
