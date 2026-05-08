using System.Text.Json;
using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;

namespace Framework.Api.Services.IapStore;

// Google Play AndroidPublisherService 초기화 공유 팩토리
// Verifier와 Consumer가 동일한 초기화 로직을 재사용 — 중복 제거 목적
// Singleton으로 등록: Lazy<GoogleCredential>을 통해 서비스 계정 인증 정보를 최초 1회만 로드
public class GooglePlayClientFactory
{
    // AndroidPublisherService 타임아웃 — Unity 클라이언트 60초 미만 안전 마진
    // Google API 서버 응답이 지연될 경우 클라이언트가 무한 대기하지 않도록 제한
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

    // 서비스 계정 JSON 파일 경로 — 생성자에서 검증 후 저장
    private readonly string _serviceAccountJsonPath;

    // Google Play 패키지명 — 생성자에서 검증 후 저장
    private readonly string _packageName;

    // 서비스 계정 인증 정보 — 최초 Create() 호출 시 1회만 파일에서 로드
    // ExecutionAndPublication: 멀티스레드 환경에서 중복 초기화 방지 (기본값과 동일)
    private readonly Lazy<GoogleCredential> _credential;

    // 생성 시점에 필수 설정값을 검증 — 잘못된 설정으로 런타임에 실패하는 상황 방지
    public GooglePlayClientFactory(IConfiguration config)
    {
        // 서비스 계정 JSON 경로 필수 검증
        _serviceAccountJsonPath = config["Iap:Google:ServiceAccountJsonPath"]
            ?? throw new InvalidOperationException("Google Play ServiceAccountJsonPath가 설정되지 않았습니다.");

        // 패키지명 필수 검증
        _packageName = config["Iap:Google:PackageName"]
            ?? throw new InvalidOperationException("Google Play PackageName이 설정되지 않았습니다.");

        // 인증 정보 지연 초기화 — 실제 API 호출 전까지 파일 I/O 발생하지 않음
        _credential = new Lazy<GoogleCredential>(
            LoadCredential,
            LazyThreadSafetyMode.ExecutionAndPublication
        );
    }

    // 서비스 계정 JSON 파일에서 GoogleCredential 생성 — Lazy 초기화 람다 구현
    // 동기 I/O 사용: Lazy<T> 내부는 비동기 컨텍스트를 안전하게 다루기 어려움
    private GoogleCredential LoadCredential()
    {
        // 서비스 계정 JSON 파일을 동기로 읽기 — Lazy 초기화 내부는 동기 필수
        var jsonContent = File.ReadAllText(_serviceAccountJsonPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        // 서비스 계정의 이메일 및 비공개 키 추출
        var clientEmail = root.GetProperty("client_email").GetString()
            ?? throw new InvalidOperationException("서비스 계정 JSON에 client_email이 없습니다.");
        var privateKey = root.GetProperty("private_key").GetString()
            ?? throw new InvalidOperationException("서비스 계정 JSON에 private_key가 없습니다.");

        // ServiceAccountCredential → GoogleCredential 래핑
        return new ServiceAccountCredential(
            new ServiceAccountCredential.Initializer(clientEmail)
            {
                Scopes = new[] { AndroidPublisherService.Scope.Androidpublisher }
            }.FromPrivateKey(privateKey)
        ).ToGoogleCredential();
    }

    // AndroidPublisherService 인스턴스 생성 — 호출자가 using으로 dispose 책임
    // _credential.Value: 최초 호출 시에만 LoadCredential 실행, 이후 캐시된 인증 정보 재사용
    // AndroidPublisherService 자체는 캐싱하지 않음 — 호출당 신규 생성
    public AndroidPublisherService Create()
    {
        // 캐시된 인증 정보를 주입하여 API 클라이언트 초기화
        var service = new AndroidPublisherService(new BaseClientService.Initializer
        {
            HttpClientInitializer = _credential.Value,
            ApplicationName = "FrameworkGameServer"
        });

        // Google.Apis 내부 HttpClient 타임아웃 직접 설정
        service.HttpClient.Timeout = HttpTimeout;

        return service;
    }

    // appsettings에서 미리 검증된 PackageName 반환 — Verifier/Consumer 공통 사용
    public string GetPackageName() => _packageName;
}
