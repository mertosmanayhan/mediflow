using MediFlow.BuildingBlocks.Common;

namespace MediFlow.PatientProfile.Domain.Tests;

public class PatientTests
{
    // Sabit bir "bugün". Gerçek saati okumadığımız için testler
    // yarın da, bir yıl sonra da aynı sonucu verir.
    private static readonly DateOnly Today = new(2026, 9, 30);

    [Fact]
    public void Create_Should_Fail_When_FirstNameIsEmpty()
    {
        Result<Patient> result = Patient.Create("", "Demir", new DateOnly(1990, 5, 12), Today);

        Assert.True(result.IsFailure);
        Assert.Equal("Patient.FirstNameEmpty", result.Error.Code);
    }

    [Fact]
    public void Create_Should_Fail_When_LastNameIsEmpty()
    {
        Result<Patient> result = Patient.Create("Mehmet", "   ", new DateOnly(1990, 5, 12), Today);

        Assert.True(result.IsFailure);
        Assert.Equal("Patient.LastNameEmpty", result.Error.Code);
    }

    [Fact]
    public void Create_Should_Fail_When_BirthDateIsInTheFuture()
    {
        DateOnly tomorrow = Today.AddDays(1);

        Result<Patient> result = Patient.Create("Mehmet", "Demir", tomorrow, Today);

        Assert.True(result.IsFailure);
        Assert.Equal("Patient.BirthDateInFuture", result.Error.Code);
    }

    [Fact]
    public void Create_Should_Succeed_When_DataIsValid()
    {
        Result<Patient> result = Patient.Create("Mehmet", "Demir", new DateOnly(1990, 5, 12), Today);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("Mehmet", result.Value.FirstName);
        Assert.Equal(new DateOnly(1990, 5, 12), result.Value.BirthDate);
    }

    [Fact]
    public void Create_Should_TrimWhitespace_FromNames()
    {
        // Kullanıcı kutuya "  Mehmet  " yazarsa veritabanına öyle gitmesin.
        Result<Patient> result = Patient.Create("  Mehmet  ", "  Demir  ", new DateOnly(1990, 5, 12), Today);

        Assert.True(result.IsSuccess);
        Assert.Equal("Mehmet", result.Value.FirstName);
        Assert.Equal("Demir", result.Value.LastName);
    }

    [Fact]
    public void Create_Should_Allow_BirthDateOfToday()
    {
        // Sınır durumu: bugün doğan bebek. "Gelecekte olamaz" kuralı
        // bugünü DIŞLAMAMALI. Sınırları test etmek alışkanlık olsun.
        Result<Patient> result = Patient.Create("Bebek", "Demir", Today, Today);

        Assert.True(result.IsSuccess);
    }
}
