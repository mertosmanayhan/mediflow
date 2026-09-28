namespace MediFlow.BuildingBlocks.Common.Tests;

/// <summary>
/// Documents the contract of <see cref="Result"/> and <see cref="Result{TValue}"/>.
/// Every test follows Arrange / Act / Assert.
/// </summary>
public class ResultTests
{
    // ---------------------------------------------------------------
    // Result (no value)
    // ---------------------------------------------------------------

    [Fact]
    public void Success_Should_BeSuccessful_And_CarryNoError()
    {
        // Act
        Result result = Result.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_Should_BeFailure_And_CarryTheGivenError()
    {
        // Arrange
        Error error = new("Patient.NotFound", "Patient was not found.");

        // Act
        Result result = Result.Failure(error);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    // A Theory runs the same test body once per data row. It keeps us from
    // copy-pasting near-identical Facts, and each row is reported separately
    // so a failure points at the exact input.
    [Theory]
    [InlineData("Patient.NotFound", "Patient was not found.")]
    [InlineData("Appointment.SlotTaken", "The selected slot is no longer available.")]
    [InlineData("Lab.ResultNotReady", "Lab result is not available yet.")]
    public void Failure_Should_PreserveCodeAndMessage(string code, string message)
    {
        // Act
        Result result = Result.Failure(new Error(code, message));

        // Assert
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(message, result.Error.Message);
    }

    // ---------------------------------------------------------------
    // Result<TValue>
    // ---------------------------------------------------------------

    [Fact]
    public void SuccessOfT_Should_ExposeTheValue()
    {
        // Arrange
        Guid patientId = Guid.NewGuid();

        // Act
        Result<Guid> result = Result.Success(patientId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(patientId, result.Value);
    }

    [Fact]
    public void FailureOfT_Should_Throw_When_ValueIsAccessed()
    {
        // Arrange
        Result<Guid> result = Result.Failure<Guid>(
            new Error("Patient.NotFound", "Patient was not found."));

        // Act + Assert
        // Reading the value of a failed result means the caller skipped the
        // IsSuccess check. We want that to be a loud bug, not a silent Guid.Empty.
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => result.Value);

        Assert.Contains("Check IsSuccess first", exception.Message);
    }

    [Fact]
    public void ResultOfT_Should_BeUsableAsResult()
    {
        // Result<TValue> inherits Result, so a generic result can flow through
        // code that only cares about success or failure (e.g. logging pipelines).

        // Act
        Result asBaseType = Result.Success(42);

        // Assert
        Assert.True(asBaseType.IsSuccess);
        Assert.IsType<Result<int>>(asBaseType);
    }

    // ---------------------------------------------------------------
    // Error
    // ---------------------------------------------------------------

    [Fact]
    public void Error_Should_UseValueEquality_Because_ItIsARecord()
    {
        // Arrange - two distinct instances with identical content
        Error first = new("Appointment.SlotTaken", "Slot is taken.");
        Error second = new("Appointment.SlotTaken", "Slot is taken.");

        // Assert
        Assert.Equal(first, second);   // value equality
        Assert.True(first == second);  // record also rewrites ==
        Assert.NotSame(first, second); // but they are still two objects in memory
    }

    [Fact]
    public void ErrorNone_Should_RepresentAbsenceOfError()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Message);
    }
}
