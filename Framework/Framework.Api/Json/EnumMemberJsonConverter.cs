using System.Text.Json;
using System.Text.Json.Serialization;

namespace Framework.Api.Json;

/// <summary>
/// enum 필드 역직렬화 시 유효하지 않은 값을 EnumDeserializationException으로 변환하는 커스텀 JsonConverter.
/// 기본 JsonStringEnumConverter는 잘못된 값을 조용히 무시하거나 JsonException을 던지는데,
/// 이 컨버터는 클라이언트에게 명확한 400 응답을 돌려주기 위해 전용 예외를 발생시킨다.
/// </summary>
public class EnumMemberJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    // 기반 역직렬화를 위임할 표준 컨버터 (JsonStringEnumConverter 내부 컨버터 재사용)
    private static readonly JsonConverter<TEnum> _baseConverter;

    // 허용된 값 목록 — 클라이언트에게 안내용 (camelCase 문자열)
    private static readonly string[] _allowedValues;

    /// <summary>
    /// static 생성자: _baseConverter와 _allowedValues를 한 번만 초기화한다.
    /// </summary>
    static EnumMemberJsonConverter()
    {
        // CamelCase 정책을 적용한 JsonStringEnumConverter로부터 내부 컨버터를 캐싱
        var factory = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        var options = new JsonSerializerOptions();
        _baseConverter = (JsonConverter<TEnum>)factory.CreateConverter(typeof(TEnum), options);

        // 허용된 값 목록을 camelCase로 변환하여 캐싱
        _allowedValues = Enum.GetNames<TEnum>()
            .Select(name => JsonNamingPolicy.CamelCase.ConvertName(name))
            .ToArray();
    }

    /// <summary>
    /// JSON 읽기: 유효하지 않은 enum 값이면 EnumDeserializationException을 던진다.
    /// String 토큰과 Number 토큰 모두 처리한다.
    /// </summary>
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 클라이언트가 보낸 원본 값을 보존 (예외 메시지 생성용)
        // Utf8JsonReader.GetRawText()는 .NET에 없으므로 TokenType별로 직접 추출
        string rawValue = reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? "",
            JsonTokenType.Number => reader.TryGetInt64(out var n) ? n.ToString() : reader.GetDouble().ToString(),
            _ => reader.TokenType.ToString()
        };

        try
        {
            // 기반 컨버터에 역직렬화 위임 — 내부적으로 복사된 reader 사용
            return _baseConverter.Read(ref reader, typeToConvert, options);
        }
        catch
        {
            // 역직렬화 실패 시 전용 예외로 변환 (field는 미들웨어에서 JsonException.Path로 채워짐)
            throw new EnumDeserializationException(
                receivedValue: rawValue,
                expectedType: typeof(TEnum).Name,
                allowedValues: _allowedValues
            );
        }
    }

    /// <summary>
    /// JSON 쓰기: 기반 컨버터에 위임 (직렬화 동작은 변경하지 않음)
    /// </summary>
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        _baseConverter.Write(writer, value, options);
    }
}
