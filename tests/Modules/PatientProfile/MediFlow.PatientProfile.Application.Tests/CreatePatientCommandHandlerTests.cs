using MediFlow.BuildingBlocks.Common;
using MediFlow.PatientProfile.Application.Patients.CreatePatient;

namespace MediFlow.PatientProfile.Application.Tests;

public class CreatePatientCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    private readonly InMemoryPatientRepository _patients = new();
    private readonly CreatePatientCommandHandler _handler;

    public CreatePatientCommandHandlerTests() =>
        _handler = new CreatePatientCommandHandler(_patients, new FixedClock(Today));

    private static CreatePatientCommand ValidCommand() =>
        new("Mehmet", "Demir", new DateOnly(1990, 5, 12));

    [Fact]
    public async Task Handle_Should_SaveThePatient_When_CommandIsValid()
    {
        Result<Guid> result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_patients.Saved);
        Assert.Equal("Mehmet", _patients.Saved[0].FirstName);
    }

    [Fact]
    public async Task Handle_Should_ReturnTheIdOfTheSavedPatient()
    {
        Result<Guid> result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(_patients.Saved[0].Id, result.Value);
    }

    [Fact]
    public async Task Handle_Should_NotSaveAnything_When_DomainRulesReject()
    {
        // DİKKAT: bu komut doğrulayıcıdan geçemezdi, ama handler'ı DOĞRUDAN
        // çağırıyoruz - yani pipeline yok. Domain'in son savunma hattı
        // olarak hâlâ çalıştığını kanıtlıyor.
        var invalid = new CreatePatientCommand("", "Demir", new DateOnly(1990, 5, 12));

        Result<Guid> result = await _handler.Handle(invalid, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_patients.Saved);
    }

    [Fact]
    public async Task Handle_Should_PassTheDomainErrorThrough_Unchanged()
    {
        var invalid = new CreatePatientCommand("Mehmet", "   ", new DateOnly(1990, 5, 12));

        Result<Guid> result = await _handler.Handle(invalid, CancellationToken.None);

        Assert.Equal("Patient.LastNameEmpty", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_UseTheInjectedClock()
    {
        DateOnly tomorrow = Today.AddDays(1);
        var command = new CreatePatientCommand("Mehmet", "Demir", tomorrow);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Patient.BirthDateInFuture", result.Error.Code);
        Assert.Empty(_patients.Saved);
    }

    [Fact]
    public async Task SavedPatient_Should_BeRetrievableById()
    {
        Result<Guid> created = await _handler.Handle(ValidCommand(), CancellationToken.None);

        var found = await _patients.GetByIdAsync(created.Value);

        Assert.NotNull(found);
        Assert.Equal(created.Value, found.Id);
    }
}
