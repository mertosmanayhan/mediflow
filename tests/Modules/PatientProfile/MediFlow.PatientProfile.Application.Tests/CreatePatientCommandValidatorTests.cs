using FluentValidation.Results;
using MediFlow.PatientProfile.Application.Patients.CreatePatient;
using MediFlow.PatientProfile.Domain;

namespace MediFlow.PatientProfile.Application.Tests;

public class CreatePatientCommandValidatorTests
{
    private readonly CreatePatientCommandValidator _validator = new();

    private static CreatePatientCommand ValidCommand() =>
        new("Mehmet", "Demir", new DateOnly(1990, 5, 12));

    [Fact]
    public void Validate_Should_Pass_When_CommandIsValid()
    {
        ValidationResult result = _validator.Validate(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Patient.FirstNameEmpty")]
    [InlineData("   ", "Patient.FirstNameEmpty")]
    public void Validate_Should_Reject_EmptyFirstName(string firstName, string expectedCode)
    {
        ValidationResult result = _validator.Validate(ValidCommand() with { FirstName = firstName });

        Assert.False(result.IsValid);
        Assert.Contains(expectedCode, result.Errors.Select(error => error.ErrorCode));
    }

    [Fact]
    public void Validate_Should_Reject_NameLongerThanTheLimit()
    {
        // Sınırın BİR FAZLASI. Sınır değerini test etmek alışkanlık olsun:
        // hatalar tam sınırda saklanır.
        string tooLong = new('a', Patient.NameMaxLength + 1);

        ValidationResult result = _validator.Validate(ValidCommand() with { LastName = tooLong });

        Assert.False(result.IsValid);
        Assert.Contains("Patient.LastNameTooLong", result.Errors.Select(error => error.ErrorCode));
    }

    [Fact]
    public void Validate_Should_Accept_NameExactlyAtTheLimit()
    {
        string atLimit = new('a', Patient.NameMaxLength);

        ValidationResult result = _validator.Validate(ValidCommand() with { LastName = atLimit });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Should_Reject_MissingBirthDate()
    {
        // JSON'da birthDate alanı hiç gönderilmezse default(DateOnly) gelir.
        ValidationResult result = _validator.Validate(ValidCommand() with { BirthDate = default });

        Assert.False(result.IsValid);
        Assert.Contains("Patient.BirthDateMissing", result.Errors.Select(error => error.ErrorCode));
    }

    [Fact]
    public void Validate_Should_ReportAllFailures_NotJustTheFirst()
    {
        // Doğrulayıcının varlık sebebi: kullanıcıya TÜM eksikleri bir
        // seferde söylemek. Domain ilk hatada durur, bu durmaz.
        var command = new CreatePatientCommand("", "", default);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }
}
