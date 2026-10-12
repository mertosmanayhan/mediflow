using MediatR;
using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Application.Patients.CreatePatient;
using MediFlow.PatientProfile.Application.Patients.GetPatientById;

namespace MediFlow.Api.Endpoints;

internal static class PatientEndpoints
{
    internal static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        // RequireAuthorization(): bu gruptaki TÜM endpoint'ler geçerli bir
        // jeton ister. Tek tek işaretlemek yerine grupta tanımlamak,
        // "yeni endpoint ekleyip korumayı unutma" hatasını engelliyor -
        // güvenlikte varsayılan KAPALI olmalı, açık değil.
        RouteGroupBuilder group = app.MapGroup("/patients")
            .WithTags("Patients")
            .RequireAuthorization();

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
            .WithSummary("Yeni hasta kaydı oluşturur");

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
            .WithSummary("Kimliğe göre hasta getirir");
    }
}
