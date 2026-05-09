using Framework.Api.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using System.IdentityModel.Tokens.Jwt;

namespace Framework.Api.Services.IapStore;

// Google Pub/Sub OIDC 토큰 검증기
// Google Cloud Pub/Sub이 푸시 요청 시 Authorization 헤더에 OIDC 토큰을 포함
// Google JWKS를 통해 서명 검증 → 위조 요청 차단
//
// [static→instance 변경 이유]
// 기존 static ConfigurationManager는 DI 생명주기 바깥에 있어 Polly 파이프라인 적용 불가.
// Singleton 인스턴스로 전환하여 프로세스 전역 JWKS 캐시는 동일하게 유지하면서 Polly를 주입받음.
// R-3: 첫 RTDN 시 JWKS 재페치 1회 발생 — 인지 후 수용.
public class GooglePubSubAuthenticator
{
    // Google OIDC 공개키(JWKS) 설정 관리자 — 인스턴스 필드 (JWKS 자동 갱신 + 캐시 유지)
    // 기존 static에서 인스턴스로 변경: Singleton으로 등록하므로 프로세스 전역 캐시 효과 동일
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

    private readonly IConfiguration _config;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly ILogger<GooglePubSubAuthenticator> _logger;

    public GooglePubSubAuthenticator(
        IConfiguration config,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<GooglePubSubAuthenticator> logger)
    {
        _config = config;
        _pipelineProvider = pipelineProvider;
        _logger = logger;

        // JWKS ConfigurationManager 초기화 — Singleton 생성 시 1회만 실행
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://accounts.google.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());
    }

    // Authorization 헤더 값을 받아 Google OIDC 토큰 유효성 검증
    // authorizationHeader: "Bearer eyJ..." 형식의 전체 헤더 값
    // 반환값: 검증 성공 시 true, 토큰 자체가 잘못된 경우(검증 실패·만료) false
    // 예외 발생: JWKS fetch 등 인프라 장애 시 → 호출자(IapRtdnController)에서 503 매핑
    public async Task<bool> ValidateAsync(string authorizationHeader)
    {
        // "Bearer " 접두어 제거하여 순수 JWT 토큰 추출
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Pub/Sub OIDC 검증 실패 — Authorization 헤더가 Bearer 형식이 아님");
            return false;
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();

        // appsettings.json에서 허용 Audience(서비스 URL) 읽기
        var audience = _config["Iap:Google:RtdnAudience"];
        if (string.IsNullOrWhiteSpace(audience))
        {
            _logger.LogError("Pub/Sub OIDC 검증 실패 — appsettings에 Iap:Google:RtdnAudience가 없음");
            return false;
        }

        // Polly "external-google-auth" 파이프라인으로 JWKS fetch에 타임아웃·서킷브레이커 적용
        // 인프라 장애(타임아웃·서킷 OPEN)는 예외로 전파 → 컨트롤러에서 503 반환 (D-3)
        var pipeline = _pipelineProvider.GetPipeline(ResilienceExtensions.GoogleAuthKey);

        OpenIdConnectConfiguration openIdConfig;
        try
        {
            openIdConfig = await pipeline.ExecuteAsync(async ct =>
            {
                // Google OIDC Discovery Document에서 서명 공개키(JWKS) 가져오기 (캐시 활용)
                return await _configManager.GetConfigurationAsync(ct);
            });
        }
        catch (BrokenCircuitException ex)
        {
            // 서킷브레이커 OPEN — Google JWKS 서버 연속 장애 상태
            // 인프라 장애이므로 예외를 전파하여 컨트롤러가 503 반환하도록 (D-3)
            _logger.LogError(ex, "Pub/Sub OIDC JWKS fetch 서킷브레이커 OPEN — Google 서버 장애");
            throw;
        }
        catch (TimeoutRejectedException ex)
        {
            // JWKS fetch 타임아웃 — 인프라 장애이므로 예외 전파하여 503 반환 (D-3)
            _logger.LogError(ex, "Pub/Sub OIDC JWKS fetch 타임아웃 초과");
            throw;
        }
        catch (Exception ex)
        {
            // 그 외 JWKS fetch 오류 — 인프라 장애로 간주하여 예외 전파
            _logger.LogError(ex, "Pub/Sub OIDC JWKS fetch 예기치 않은 오류");
            throw;
        }

        // JWKS 가져온 후 JWT 토큰 자체 검증 — 검증 실패는 false 반환 (재시도 무의미)
        return ValidateToken(token, audience, openIdConfig);
    }

    // JWT 토큰 서명·만료·발급자·Audience 검증 — 검증 실패 시 false 반환
    // 인프라와 무관한 순수 로컬 연산이므로 예외를 던지지 않고 bool로 처리
    private bool ValidateToken(
        string token,
        string audience,
        OpenIdConnectConfiguration openIdConfig)
    {
        var validationParameters = new TokenValidationParameters
        {
            // 발급자 검증 — Google 외 다른 발급자의 토큰 차단
            ValidIssuer = "https://accounts.google.com",
            ValidateIssuer = true,

            // Audience 검증 — 이 서버의 RTDN 엔드포인트 URL과 일치해야 함
            ValidAudience = audience,
            ValidateAudience = true,

            // 만료 시간 검증 — 만료된 토큰은 재생 공격에 활용될 수 있음
            ValidateLifetime = true,

            // Google JWKS 공개키로 서명 검증
            IssuerSigningKeys = openIdConfig.SigningKeys,
            ValidateIssuerSigningKey = true,
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning(ex, "Pub/Sub OIDC 검증 실패 — 토큰 만료");
            return false;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Pub/Sub OIDC 검증 실패 — 토큰 검증 오류");
            return false;
        }
    }
}
