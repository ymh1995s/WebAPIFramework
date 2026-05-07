namespace Framework.Domain.Common.Guards;

using System.Runtime.CompilerServices;

// 메서드 사전 조건 단언 헬퍼 — 위반 시 ArgumentException 계열 throw.
// CallerArgumentExpression으로 호출부 paramName 자동 추출하여 nameof() 반복 제거.
public static class Guard
{
    // null 참조 금지 — null이면 ArgumentNullException
    public static T NotNull<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
        => value ?? throw new ArgumentNullException(paramName);

    // 빈 문자열 금지 — null 또는 공백이면 ArgumentException
    public static string NotNullOrWhiteSpace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("값이 비어 있습니다.", paramName);
        return value;
    }

    // 최대 길이 제한 — 초과 시 ArgumentException
    public static string MaxLength(
        string value, int max,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value.Length > max)
            throw new ArgumentException(
                $"최대 {max}자까지 허용됩니다. (현재: {value.Length}자)", paramName);
        return value;
    }

    // 범위 검증 — [min, max] 밖이면 ArgumentOutOfRangeException
    public static T InRange<T>(
        T value, T min, T max,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(paramName,
                $"값은 [{min}, {max}] 범위여야 합니다. (현재: {value})");
        return value;
    }
}
