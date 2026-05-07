namespace Framework.Domain.Common.Results;

// 값 없는 작업 결과 — 성공/실패 여부와 실패 사유를 타입 안전하게 전달
public readonly struct Result
{
    public bool  IsSuccess { get; }
    public bool  IsFailure => !IsSuccess;
    // 실패 사유 — IsSuccess가 true일 때는 Error.None
    public Error Error     { get; }

    private Result(bool ok, Error error) { IsSuccess = ok; Error = error; }

    public static Result Success()            => new(true,  Error.None);
    public static Result Failure(Error error) => new(false, error);

    // 성공/실패 분기 처리 헬퍼
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
        => IsSuccess ? onSuccess() : onFailure(Error);
}

// 값을 포함한 작업 결과 — 성공 시 T 값, 실패 시 Error 반환
public readonly struct Result<T>
{
    public bool  IsSuccess { get; }
    public bool  IsFailure => !IsSuccess;
    // 성공 값 — IsSuccess가 true일 때만 유효
    public T?    Value     { get; }
    // 실패 사유 — IsSuccess가 false일 때만 유효
    public Error Error     { get; }

    private Result(bool ok, T? value, Error error)
    { IsSuccess = ok; Value = value; Error = error; }

    public static Result<T> Success(T value)       => new(true,  value,   Error.None);
    public static Result<T> Failure(Error error)   => new(false, default, error);

    // 암시적 변환 — return value; 또는 return Error.NotFound; 형태 허용
    public static implicit operator Result<T>(T value)  => Success(value);
    public static implicit operator Result<T>(Error e)  => Failure(e);

    // 성공/실패 분기 처리 헬퍼
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error);
}
