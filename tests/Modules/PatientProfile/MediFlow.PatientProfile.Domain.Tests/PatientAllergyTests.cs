using MediFlow.BuildingBlocks.Common;

namespace MediFlow.PatientProfile.Domain.Tests;

public class PatientAllergyTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    // Her testte 4 satır hasta oluşturma kodu tekrarlamamak için.
    // Testin ASIL konusu alerjiler; hasta oluşturmak sadece hazırlık.
    private static Patient ValidPatient() =>
        Patient.Create("Mehmet", "Demir", new DateOnly(1990, 5, 12), Today).Value;

    [Fact]
    public void NewPatient_Should_HaveNoAllergies()
    {
        Patient patient = ValidPatient();

        Assert.Empty(patient.Allergies);
    }

    [Fact]
    public void AddAllergy_Should_AddTheAllergy()
    {
        Patient patient = ValidPatient();

        Result result = patient.AddAllergy("Penisilin", AllergySeverity.High);

        Assert.True(result.IsSuccess);
        Assert.Single(patient.Allergies);
        Assert.Equal("Penisilin", patient.Allergies[0].Name);
        Assert.Equal(AllergySeverity.High, patient.Allergies[0].Severity);
    }

    [Fact]
    public void AddAllergy_Should_Fail_When_NameIsEmpty()
    {
        Patient patient = ValidPatient();

        Result result = patient.AddAllergy("   ", AllergySeverity.Low);

        Assert.True(result.IsFailure);
        Assert.Equal("Patient.AllergyNameEmpty", result.Error.Code);
        Assert.Empty(patient.Allergies);
    }

    [Fact]
    public void AddAllergy_Should_Fail_When_AllergyAlreadyAdded()
    {
        Patient patient = ValidPatient();
        patient.AddAllergy("Penisilin", AllergySeverity.High);

        Result result = patient.AddAllergy("Penisilin", AllergySeverity.Low);

        Assert.True(result.IsFailure);
        Assert.Equal("Patient.AllergyAlreadyExists", result.Error.Code);
        // Ve liste bozulmadı: hâlâ tek kayıt, hâlâ ilk şiddet değeri
        Assert.Single(patient.Allergies);
        Assert.Equal(AllergySeverity.High, patient.Allergies[0].Severity);
    }

    [Fact]
    public void AddAllergy_Should_TreatDifferentCasingAsTheSameAllergy()
    {
        Patient patient = ValidPatient();
        patient.AddAllergy("Penisilin", AllergySeverity.High);

        Result result = patient.AddAllergy("PENISILIN", AllergySeverity.High);

        Assert.True(result.IsFailure);
        Assert.Single(patient.Allergies);
    }

    [Fact]
    public void AddAllergy_Should_TrimTheName()
    {
        Patient patient = ValidPatient();

        patient.AddAllergy("  Aspirin  ", AllergySeverity.Moderate);

        Assert.Equal("Aspirin", patient.Allergies[0].Name);
    }

    [Fact]
    public void AddAllergy_Should_AllowSeveralDifferentAllergies()
    {
        Patient patient = ValidPatient();

        patient.AddAllergy("Penisilin", AllergySeverity.High);
        patient.AddAllergy("Aspirin", AllergySeverity.Moderate);
        patient.AddAllergy("Fıstık", AllergySeverity.LifeThreatening);

        Assert.Equal(3, patient.Allergies.Count);
    }

    [Fact]
    public void Allergies_Should_NotBeModifiableFromOutside()
    {
        // Liste dışarıya IReadOnlyList olarak veriliyor. Ama sadece arayüzü
        // değiştirmek yetmez: içerideki List'i olduğu gibi döndürürsek çağıran
        // onu List'e geri çevirip ekleme yapabilir. Bu test o kaçağı kapatıyor.
        Patient patient = ValidPatient();

        Assert.IsNotType<List<Allergy>>(patient.Allergies);
    }
}
