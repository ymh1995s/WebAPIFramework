using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Framework.Api.Services.IapStore;

// Google Pub/Sub OIDC 토큰 검증기
// Google Cloud Pub/Sub이 푸시 요청 시 Authorization 헤더에 OIDC 토큰을 포함
// Google JWKS를 통해 서명 검증 → 위조 요청 차단
public class GooglePubSubAuthenticator
{
    // Google OIDC 공개키(JWKS) 설정 관리자 — 정적 필드로 캐싱하여 매 요청마다 재생성 방지
    // OpenIdConnectConfiguration은 JWKS 키를 자동으로 가져오고 갱신함
    private static readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager =
        new(
            "https://accounts.google.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());

    private readonly IConfiguration _config;
    private readonly ILogger<GooglePubSubAuthenticator> _logger;

    public GooglePubSubAuthenticator(IConfiguration config, ILogger<GooglePubSubAuthenticator> logger)
    {
        _config = config;
        _logger = logger;
    }

    // Authorization 헤더 값을 받아 Google OIDC 토큰 유효성 검증
    // authorizationHeader: "Bearer eyJ..." 형식의 전체 헤더 값
    // 반환값: 검증 성공 시 true, 실패 시 false
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

        try
        {
            // Google OIDC Discovery Document에서 서명 공개키(JWKS) 가져오기 (캐시 활용)
            var openIdConfig = await _configManager.GetConfigurationAsync(CancellationToken.None);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pub/Sub OIDC 검증 중 예기치 않은 오류 발생");
            return false;
        }
    }
}
