namespace BjjJournal.Domain.Common;

/// <summary>Resultado de una operación que puede fallar por validación. Las excepciones quedan para fallos inesperados.</summary>
public class Result
{
    protected Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public string? Error { get; }

    public static Result Ok() => new(true, null);
    public static Result Fail(string error) => new(false, error);
}
