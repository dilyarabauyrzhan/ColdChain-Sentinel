namespace ColdChainMonitor;

// A Result always contains either a success value or a failure error, never both.
public abstract record Result<T, E>
{
    private Result() { }

    public sealed record Success(T Value) : Result<T, E>;
    public sealed record Failure(E Error) : Result<T, E>;

    public static Result<T, E> Ok(T value) => new Success(value);
    public static Result<T, E> Fail(E error) => new Failure(error);

    public Result<U, E> Map<U>(Func<T, U> transform) => this switch
    {
        Success success => Result<U, E>.Ok(transform(success.Value)),
        Failure failure => Result<U, E>.Fail(failure.Error),
        _ => throw new InvalidOperationException("Unknown Result case.")
    };

    public Result<U, E> Bind<U>(Func<T, Result<U, E>> next) => this switch
    {
        Success success => next(success.Value),
        Failure failure => Result<U, E>.Fail(failure.Error),
        _ => throw new InvalidOperationException("Unknown Result case.")
    };
}

public sealed record DomainError(string Code, string Message);
