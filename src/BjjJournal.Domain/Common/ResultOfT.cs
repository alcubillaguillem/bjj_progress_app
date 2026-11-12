namespace BjjJournal.Domain.Common;

/// <summary>Resultado con valor.</summary>
public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, string? error) : base(isSuccess, error) => Value = value;

    public T? Value { get; }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static new Result<T> Fail(string error) => new(false, default, error);
}
