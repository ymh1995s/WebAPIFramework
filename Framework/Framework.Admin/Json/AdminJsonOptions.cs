using System.Text.Json;
using System.Text.Json.Serialization;

namespace Framework.Admin.Json;

/// <summary>
/// Admin Blazor 전역 JSON 직렬화 옵션.
/// API 서버의 JsonOptions와 동일한 설정을 유지하여 enum 문자열 변환 불일치를 방지한다.
/// </summary>
public static class AdminJsonOptions
{
    /// <summary>
    /// 기본 JSON 직렬화 옵션.
    /// - enum 값을 camelCase 문자열로 변환 (예: GooglePlay → "googlePlay")
    /// - 속성명 대소문자 무시 역직렬화
    /// - 속성명 camelCase 직렬화
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        // enum을 camelCase 문자열로 직렬화/역직렬화
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        // 역직렬화 시 속성명 대소문자 무시
        PropertyNameCaseInsensitive = true,
        // 직렬화 시 camelCase 속성명 사용
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
