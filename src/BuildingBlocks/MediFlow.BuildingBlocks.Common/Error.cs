namespace MediFlow.BuildingBlocks.Common;

/// <summary>
/// Describes a single failure in a stable, transportable way.
/// </summary>
/// <param name="Code">
/// Machine-readable, stable identifier such as <c>Appointment.SlotTaken</c>.
/// Clients branch on this, HTTP status mapping is derived from it, and it must
/// never change once released - it is part of the API contract.
/// </param>
/// <param name="Message">
/// Human-readable description, intended for developers and logs.
/// End-user text is resolved from <paramref name="Code"/> by the presentation
/// layer, so this string is safe to change at any time.
/// </param>
public sealed record Error(string Code, string Message)
{
    /// <summary>
    /// The absence of an error. Every successful <see cref="Result"/> carries this.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
