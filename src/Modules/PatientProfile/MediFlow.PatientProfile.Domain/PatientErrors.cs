using MediFlow.BuildingBlocks.Common;

namespace MediFlow.PatientProfile.Domain;

/// <summary>
/// Every way creating or changing a patient can fail, in one place.
/// </summary>
/// <remarks>
/// Collecting them here rather than inlining strings at the call site means the
/// codes stay unique, are easy to review as a set, and can be mapped to HTTP
/// status codes later without hunting through the domain.
/// </remarks>
public static class PatientErrors
{
    public static readonly Error FirstNameEmpty =
        new("Patient.FirstNameEmpty", "First name is required.");

    public static readonly Error LastNameEmpty =
        new("Patient.LastNameEmpty", "Last name is required.");

    public static readonly Error BirthDateInFuture =
        new("Patient.BirthDateInFuture", "Birth date cannot be in the future.");

    public static readonly Error NotFound =
        new("Patient.NotFound", "No patient exists with the given id.");

    public static readonly Error AllergyNameEmpty =
        new("Patient.AllergyNameEmpty", "Allergy name is required.");

    public static readonly Error AllergyAlreadyExists =
        new("Patient.AllergyAlreadyExists", "This allergy is already on the patient's record.");
}
