using System.Text.Json;
using System.Text.Json.Serialization;

namespace Framework.Api.Json;

/// <summary>
/// 모든 enum 타입에 대해 EnumMemberJsonConverter를 생성하는 팩토리.
/// AddJsonOptions에서 JsonStringEnumConverter 대신 이 팩토리를 등록하면
/// 모든 enum 필드에 대한 역직렬화 오류가 EnumDeserializationException으로 전환된다.
/// </summary>
public class EnumMemberJsonConverterFactory : JsonConverterFactory
{
    /// <summary>
    /// enum 타입에 대해서만 이 팩토리가 처리 가능함을 선언한다.
    /// </summary>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    /// <summary>
    /// 요청된 enum 타입에 맞는 EnumMemberJsonConverter 인스턴스를 생성하여 반환한다.
    /// </summary>
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        // EnumMemberJsonConverter<TEnum>을 리플렉션으로 인스턴스화
        var converterType = typeof(EnumMemberJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}
