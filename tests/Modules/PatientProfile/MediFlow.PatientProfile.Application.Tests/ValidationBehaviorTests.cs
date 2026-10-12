using FluentValidation;
using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Application.Behaviors;
using MediFlow.PatientProfile.Application.Patients.CreatePatient;

namespace MediFlow.PatientProfile.Application.Tests;

/// <summary>
/// Pipeline'ın asıl vaadini test eder: geçersiz girdi handler'a ULAŞMAZ.
/// </summary>
public class ValidationBehaviorTests
{
    private static readonly CreatePatientCommand Valid =
        new("Mehmet", "Demir", new DateOnly(1990, 5, 12));

    private static readonly CreatePatientCommand Invalid =
        new("", "", default);

    private static ValidationBehavior<CreatePatientCommand, Result<Guid>> Behavior(
        params IValidator<CreatePatientCommand>[] validators) => new(validators);

    [Fact]
    public async Task Handle_Should_NotReachTheHandler_When_ValidationFails()
    {
        bool handlerWasCalled = false;

        Task<Result<Guid>> Next(CancellationToken _)
        {
            handlerWasCalled = true;
            return Task.FromResult(Result.Success(Guid.NewGuid()));
        }

        Result<Guid> result = await Behavior(new CreatePatientCommandValidator())
            .Handle(Invalid, Next, CancellationToken.None);

        // Pipeline'ın varlık sebebi bu satır.
        Assert.False(handlerWasCalled);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Should_ReturnAValidationError_CarryingEveryFailure()
    {
        static Task<Result<Guid>> Next(CancellationToken _) =>
            Task.FromResult(Result.Success(Guid.NewGuid()));

        Result<Guid> result = await Behavior(new CreatePatientCommandValidator())
            .Handle(Invalid, Next, CancellationToken.None);

        ValidationError validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(ValidationError.ErrorCode, validation.Code);

        // Üç kural birden ihlal edildi: ad, soyad, doğum tarihi.
        Assert.Equal(3, validation.Errors.Count);
        Assert.Contains("Patient.FirstNameEmpty", validation.Errors.Select(error => error.Code));
        Assert.Contains("Patient.BirthDateMissing", validation.Errors.Select(error => error.Code));
    }

    [Fact]
    public async Task Handle_Should_ReachTheHandler_When_ValidationPasses()
    {
        bool handlerWasCalled = false;

        Task<Result<Guid>> Next(CancellationToken _)
        {
            handlerWasCalled = true;
            return Task.FromResult(Result.Success(Guid.NewGuid()));
        }

        Result<Guid> result = await Behavior(new CreatePatientCommandValidator())
            .Handle(Valid, Next, CancellationToken.None);

        Assert.True(handlerWasCalled);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_Should_ReachTheHandler_When_ThereAreNoValidators()
    {
        // Sorguların çoğu doğrulanmaz. Behavior onlara engel olmamalı.
        bool handlerWasCalled = false;

        Task<Result<Guid>> Next(CancellationToken _)
        {
            handlerWasCalled = true;
            return Task.FromResult(Result.Success(Guid.NewGuid()));
        }

        Result<Guid> result = await Behavior()
            .Handle(Invalid, Next, CancellationToken.None);

        Assert.True(handlerWasCalled);
        Assert.True(result.IsSuccess);
    }
}
