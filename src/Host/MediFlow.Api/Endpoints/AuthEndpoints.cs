using MediatR;
using MediFlow.BuildingBlocks.Common;
using MediFlow.Identity.Application.Authentication;

namespace MediFlow.Api.Endpoints;

internal static class AuthEndpoints
{
    internal static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/auth").WithTags("Authentication");

        group.MapPost("/register", async (
                RegisterUserCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result<Guid> result = await sender.Send(command, cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"/users/{result.Value}", new { id = result.Value })
                    : result.Error.ToHttpProblem();
            })
            .WithName("Register")
            .WithSummary("Yeni kullanıcı kaydı oluşturur");

        group.MapPost("/login", async (
                LoginCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result<AuthenticationResponse> result = await sender.Send(command, cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.Error.ToHttpProblem();
            })
            .WithName("Login")
            .WithSummary("Giriş yapar ve jeton çifti döndürür");

        group.MapPost("/refresh", async (
                RefreshTokensCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result<AuthenticationResponse> result = await sender.Send(command, cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.Error.ToHttpProblem();
            })
            .WithName("Refresh")
            .WithSummary("Yenileme jetonuyla yeni jeton çifti alır (rotasyonlu)");
    }
}
