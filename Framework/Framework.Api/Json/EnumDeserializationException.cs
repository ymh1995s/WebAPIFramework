namespace Framework.Api.Json;

/// <summary>
/// JSON 역직렬화 중 enum 필드에 허용되지 않은 값이 수신될 때 발생하는 예외.
/// 미들웨어에서 포착하여 400 ProblemDetails 응답으로 변환된다.
/// </summary>
public class EnumDeserializationException : Exception
{
    // JSON path (예: "$.outcome") — 외부에서 SetField()로 주입
    public string Field { get; private set; } = "";

    // 클라이언트가 보낸 원본 값 (256자 절단 + 제어 문자 strip 적용)
    public string ReceivedValue { get; }

    // 대상 enum 타입의 단순 이름 (예: "MatchOutcome")
    public string ExpectedType { get; }

    // 허용된 enum 값 목록 (camelCase 문자열 배열)
    public string[] AllowedValues { get; }

    /// <summary>
    /// EnumDeserializationException 생성자.
    /// receivedValue는 보안상 256자 절단 및 로그 주입 방지를 위한 제어 문자 제거가 적용된다.
    /// </summary>
    public EnumDeserializationException(
        string receivedValue,
        string expectedType,
        string[] allowedValues)
        : base($"Invalid enum value '{Sanitize(receivedValue)}' for type '{expectedType}'.")
    {
        ReceivedValue = Sanitize(receivedValue);
        ExpectedType = expectedType;
        AllowedValues = allowedValues;
    }

    /// <summary>
    /// JSON path를 외부에서 주입하는 메서드.
    /// JsonException.Path를 이용하여 어느 필드에서 오류가 발생했는지 설정한다.
    /// </summary>
    public void SetField(string path)
    {
        Field = path ?? "";
    }

    /// <summary>
    /// 수신값 정제: 256자 절단 + U+0000~U+001F 제어 문자 제거 (로그 주입 방지)
    /// </summary>
    private static string Sanitize(string? value)
    {
        if (value is null) return "";

        // 256자 초과 시 절단
        var truncated = value.Length > 256 ? value[..256] : value;

        // ASCII 제어 문자(U+0000 ~ U+001F) 제거
        return new string(truncated.Where(c => c > '').ToArray());
    }
}
