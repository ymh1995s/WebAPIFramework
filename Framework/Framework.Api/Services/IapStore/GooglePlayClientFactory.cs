using System.Text.Json;
using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Google.Apis.Services;

namespace Framework.Api.Services.IapStore;

// Google Play AndroidPublisherService 초기화 공유 팩토리
// Verifier와 Consumer가 동일한 초기화 로직을 재사용 — 중복 제거 목적
public class GooglePlayClientFactory
{
    private readonly IConfiguration _config;

    // AndroidPublisherService 타임아웃 — Unity 클라이언트 60초 미만 안전 마진
    // Google API 서버 응답이 지연될 경우 클라이언트가 무한 대기하지 않도록 제한
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

    public GooglePlayClientFactory(IConfiguration config)
    {
        _config = config;
    }

    // AndroidPublisherService 인스턴스 생성 — 호출자가 using으로 dispose 책임
    // 서비스 계정 JSON 파일을 읽어 OAuth2 인증 후 API 클라이언트 반환
    public async Task<AndroidPublisherService> CreateAsync()
    {
        // appsettings.json에서 서비스 계정 JSON 파일 경로 로드
        var serviceAccountJsonPath = _config["Iap:Google:ServiceAccountJsonPath"]
            ?? throw new InvalidOperationException("Google Play ServiceAccountJsonPath가 설정되지 않았습니다.");

        // 서비스 계정 JSON으로 ServiceAccountCredential 생성
        var jsonContent = await File.ReadAllTextAsync(serviceAccountJsonPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        // 서비스 계정의 이메일 및 비공개 키 추출
        var clientEmail = root.GetProperty("client_email").GetString()
            ?? throw new InvalidOperationException("서비스 계정 JSON에 client_email이 없습니다.");
        var privateKey = root.GetProperty("private_key").GetString()
            ?? throw new InvalidOperationException("서비스 계정 JSON에 private_key가 없습니다.");

        // ServiceAccountCredential → GoogleCredential 래핑 후 API 클라이언트 초기화
        var credential = new ServiceAccountCredential(
            new ServiceAccountCredential.Initializer(clientEmail)
            {
                Scopes = new[] { AndroidPublisherService.Scope.Androidpublisher }
            }.FromPrivateKey(privateKey)
        ).ToGoogleCredential();

        // AndroidPublisherService timeout 명시 — Unity 클라이언트 60초 미만 안전 마진
        // ConfigurableHttpClientInitializer를 통해 내부 HttpClient의 타임아웃을 30초로 설정
        var service = new AndroidPublisherService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "FrameworkGameServer"
        });

        // Google.Apis 라이브러리 내부 HttpClient에 타임아웃 직접 설정
        // AndroidPublisherService 생성 후 HttpClient 접근하여 Timeout 지정
        service.HttpClient.Timeout = HttpTimeout;

        return service;
    }

    // appsettings에서 PackageName 반환 — Verifier/Consumer 공통 사용
    public string GetPackageName()
        => _config["Iap:Google:PackageName"]
           ?? throw new InvalidOperationException("Google Play PackageName이 설정되지 않았습니다.");
}
