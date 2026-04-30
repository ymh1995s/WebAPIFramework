using Framework.Admin.Json;
using System.Net.Http.Json;

namespace Framework.Admin.Http;

/// <summary>
/// Admin Blazor 전용 HTTP 클라이언트 래퍼.
/// 모든 API 호출에 AdminJsonOptions.Default(camelCase enum 문자열)를 일관 적용한다.
/// DI에 Scoped로 등록하여 컴포넌트에서 주입받아 사용한다.
/// </summary>
public class ApiHttpClient
{
    // "ApiClient" 이름으로 등록된 HttpClient — AdminApiKeyHandler, HttpLogCaptureHandler 체인 포함
    private readonly HttpClient _httpClient;

    /// <summary>IHttpClientFactory를 통해 "ApiClient" 명명 클라이언트를 생성한다.</summary>
    public ApiHttpClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
    }

    /// <summary>
    /// GET 요청 후 응답을 역직렬화하여 반환한다.
    /// HTTP 오류 시 null 반환 (예외 전파 없음).
    /// 역직렬화 실패 시 예외 전파.
    /// </summary>
    public async Task<T?> GetAsync<T>(string url)
    {
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return default;

        // AdminJsonOptions.Default를 사용하여 enum 문자열 역직렬화 보장
        return await response.Content.ReadFromJsonAsync<T>(AdminJsonOptions.Default);
    }

    /// <summary>
    /// GET 요청 후 HttpResponseMessage 원본을 반환한다.
    /// 호출부에서 StatusCode 확인 및 직접 역직렬화가 필요한 경우 사용.
    /// </summary>
    public async Task<HttpResponseMessage> GetRawAsync(string url)
    {
        return await _httpClient.GetAsync(url);
    }

    /// <summary>
    /// POST 요청 — payload를 AdminJsonOptions.Default로 직렬화하여 전송한다.
    /// </summary>
    public async Task<HttpResponseMessage> PostAsync<T>(string url, T payload)
    {
        return await _httpClient.PostAsJsonAsync(url, payload, AdminJsonOptions.Default);
    }

    /// <summary>
    /// PUT 요청 — payload를 AdminJsonOptions.Default로 직렬화하여 전송한다.
    /// </summary>
    public async Task<HttpResponseMessage> PutAsync<T>(string url, T payload)
    {
        return await _httpClient.PutAsJsonAsync(url, payload, AdminJsonOptions.Default);
    }

    /// <summary>
    /// DELETE 요청 — 본문 없이 지정된 URL에 삭제 요청을 보낸다.
    /// </summary>
    public async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        return await _httpClient.DeleteAsync(url);
    }
}
