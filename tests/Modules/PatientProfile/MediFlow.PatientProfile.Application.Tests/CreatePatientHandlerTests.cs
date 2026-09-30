using MediFlow.BuildingBlocks.Common;

namespace MediFlow.PatientProfile.Application.Tests;

public class CreatePatientHandlerTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    private readonly InMemoryPatientRepository _patients = new();
    private readonly CreatePatientHandler _handler;

    public CreatePatientHandlerTests()
    {
        // xUnit her test için sınıfı YENİDEN oluşturur. Yani her test
        // kendi boş deposuyla başlıyor; testler birbirini etkilemiyor.
        _handler = new CreatePatientHandler(_patients, new FixedClock(Today));
    }

    private static CreatePatientRequest ValidRequest() =>
        new("Mehmet", "Demir", new DateOnly(1990, 5, 12));

    [Fact]
    public async Task HandleAsync_Should_SaveThePatient_When_RequestIsValid()
    {
        Result<Guid> result = await _handler.HandleAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Single(_patients.Saved);
        Assert.Equal("Mehmet", _patients.Saved[0].FirstName);
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnTheIdOfTheSavedPatient()
    {
        Result<Guid> result = await _handler.HandleAsync(ValidRequest());

        // Dönen kimlik, gerçekten kaydedilen hastanın kimliği olmalı -
        // rastgele bir Guid değil.
        Assert.Equal(_patients.Saved[0].Id, result.Value);
    }

    [Fact]
    public async Task HandleAsync_Should_NotSaveAnything_When_RequestIsInvalid()
    {
        var invalid = new CreatePatientRequest("", "Demir", new DateOnly(1990, 5, 12));

        Result<Guid> result = await _handler.HandleAsync(invalid);

        Assert.True(result.IsFailure);
        Assert.Empty(_patients.Saved);
    }

    [Fact]
    public async Task HandleAsync_Should_PassTheDomainErrorThrough_Unchanged()
    {
        var invalid = new CreatePatientRequest("Mehmet", "   ", new DateOnly(1990, 5, 12));

        Result<Guid> result = await _handler.HandleAsync(invalid);

        // Domain'in ürettiği hata kodu bozulmadan geldi. Bu önemli: ileride
        // HTTP durum kodunu bu koda bakarak seçeceğiz.
        Assert.Equal("Patient.LastNameEmpty", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_Should_UseTheInjectedClock()
    {
        // Sabit saatimize göre "yarın". Handler gerçek sistem saatini
        // okuyor olsaydı bu test 30 Eylül 2026'dan sonra anlamsızlaşırdı.
        DateOnly tomorrow = Today.AddDays(1);
        var request = new CreatePatientRequest("Mehmet", "Demir", tomorrow);

        Result<Guid> result = await _handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Patient.BirthDateInFuture", result.Error.Code);
        Assert.Empty(_patients.Saved);
    }

    [Fact]
    public async Task SavedPatient_Should_BeRetrievableById()
    {
        Result<Guid> created = await _handler.HandleAsync(ValidRequest());

        var found = await _patients.GetByIdAsync(created.Value);

        Assert.NotNull(found);
        Assert.Equal(created.Value, found.Id);
    }
}
