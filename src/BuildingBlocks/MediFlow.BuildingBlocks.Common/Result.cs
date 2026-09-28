namespace MediFlow.BuildingBlocks.Common;

/// <summary>
/// Outcome of an operation that can fail for an expected, business-level reason.
/// </summary>
/// <remarks>
/// Use this instead of throwing for outcomes the caller should reasonably expect
/// ("slot already taken", "patient not found"). Keep exceptions for programmer
/// mistakes and genuinely unexpected infrastructure failures.
/// </remarks>
public class Result
{
    /// <summary>
    /// Protected so that results can only be produced through the factory methods
    /// below. This keeps the success/error invariant in one place.
    /// </summary>
    protected Result(bool isSuccess, Error error)
    {
        // These two states are contradictory, and they can only happen through a
        // programmer mistake inside this assembly - never through user input.
        // That is exactly what exceptions are for: fail fast and loudly.
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException(
                "A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException(
                "A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>The operation completed as intended.</summary>
    public bool IsSuccess { get; }

    /// <summary>Convenience inverse of <see cref="IsSuccess"/>, so call sites read naturally.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The failure reason, or <see cref="Error.None"/> when <see cref="IsSuccess"/> is true.
    /// </summary>
    public Error Error { get; }

    /// <summary>Success without a value (for commands that only need to report completion).</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Failure without a value.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Success carrying a value (for queries and commands that return data).</summary>
    public static Result<TValue> Success<TValue>(TValue value) =>
        new(value, true, Error.None);

    /// <summary>Failure of an operation that would otherwise have returned a value.</summary>
    public static Result<TValue> Failure<TValue>(Error error) =>
        new(default, false, error);
}

/// <summary>
/// A <see cref="Result"/> that carries a value when successful.
/// </summary>
/// <typeparam name="TValue">Type produced on success.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    /// <summary>
    /// Internal so only <see cref="Result"/>'s factory methods can construct it.
    /// </summary>
    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    /// <summary>
    /// The produced value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result is a failure. Reading the value of a failed result is
    /// a programmer mistake: the caller skipped the <see cref="Result.IsSuccess"/>
    /// check. Throwing here turns a silent wrong answer into a loud, obvious bug.
    /// </exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            "The value of a failed result cannot be accessed. Check IsSuccess first.");
}
