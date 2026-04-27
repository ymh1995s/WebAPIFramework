using Framework.Admin.Logging;
using System.Diagnostics;

namespace Framework.Admin.Handlers;

/// <summary>
/// ApiClient의 HttpClient 파이프라인에 삽입되어
/// 모든 요청/응답을 IHttpLogStore에 기록하는 DelegatingHandler.
/// AdminApiKeyHandler 다음에 체인으로 등록한다.
/// </summary>
public class HttpLogCaptureHandler(IHttpLogStore logStore) : DelegatingHandler
{
    // IHttpLogStore — Singleton이므로 생성자 주입으로 안전하게 사용
    private readonly IHttpLogStore _logStore = logStore;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 요청 시각 기록
        var timestamp = DateTimeOffset.Now;
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            sw.Stop();

            // 정상 응답 로그 저장
            _logStore.Add(new HttpLogEntry(
                Timestamp: timestamp,
                Method: request.Method.Method,
                Url: request.RequestUri?.ToString() ?? "(unknown)",
                StatusCode: (int)response.StatusCode,
                ElapsedMs: sw.ElapsedMilliseconds,
                ErrorMessage: null
            ));

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();

            // 예외 발생 시 상태코드 0으로 기록
            _logStore.Add(new HttpLogEntry(
                Timestamp: timestamp,
                Method: request.Method.Method,
                Url: request.RequestUri?.ToString() ?? "(unknown)",
                StatusCode: 0,
                ElapsedMs: sw.ElapsedMilliseconds,
                ErrorMessage: ex.Message
            ));

            // 예외를 다시 던져 호출자가 처리하도록 함
            throw;
        }
    }
}
