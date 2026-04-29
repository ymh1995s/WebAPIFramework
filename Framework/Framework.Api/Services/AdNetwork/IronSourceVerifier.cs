using System.Security.Cryptography;
using System.Text;
using Framework.Application.Features.AdReward;
using Framework.Domain.Enums;

namespace Framework.Api.Services.AdNetwork;

// IronSource SSV(Server Side Verification) 검증기
// IronSource 공식 문서: https://developers.is.com/ironsource-mobile/general/ad-revenue-measurement-server-2-server/
// 서명 방식: HMAC-SHA256(파라미터 정렬 후 연결, secretKey) → hex 비교
public class IronSourceVerifier : IAdNetworkVerifier
{
    // 이 검증기가 담당하는 광고 네트워크
    public AdNetworkType Network => AdNetworkType.IronSource;

    private readonly IConfiguration _config;
    private readonly ILogger<IronSourceVerifier> _logger;

    public IronSourceVerifier(IConfiguration config, ILogger<IronSourceVerifier> logger)
    {
        _config = config;
        _logger = logger;
    }

    // IronSource SSV 콜백 검증
    // IronSource 파라미터: signature(서명), userId(플레이어ID), placementId, transId(거래ID), timestamp
    public Task<AdCallbackVerified> VerifyAsync(AdCallbackContext context)
    {
        // appsettings.json의 AdNetworks:IronSource:SecretKey 에서 시크릿 키 로드
        var secretKey = _config["AdNetworks:IronSource:SecretKey"]
            ?? throw new InvalidOperationException("IronSource SecretKey가 설정되지 않았습니다.");

        var queryParams = context.QueryParams;

        // 필수 파라미터 존재 확인
        if (!queryParams.TryGetValue("signature", out var providedSignature) || string.IsNullOrEmpty(providedSignature))
            throw new InvalidAdSignatureException("IronSource", "signature 파라미터가 없습니다.");

        if (!queryParams.TryGetValue("userId", out var userIdStr) || string.IsNullOrEmpty(userIdStr))
            throw new InvalidAdSignatureException("IronSource", "userId 파라미터가 없습니다.");

        if (!queryParams.TryGetValue("transId", out var transactionId) || string.IsNullOrEmpty(transactionId))
            throw new InvalidAdSignatureException("IronSource", "transId 파라미터가 없습니다.");

        if (!queryParams.TryGetValue("placementId", out var placementId) || string.IsNullOrEmpty(placementId))
            throw new InvalidAdSignatureException("IronSource", "placementId 파라미터가 없습니다.");

        // IronSource 서명: signature를 제외한 파라미터를 키 기준 오름차순 정렬 후 연결
        // 예: "placementId=xxx&timestamp=yyy&transId=zzz&userId=aaa"
        var paramString = BuildSignatureString(queryParams);

        // HMAC-SHA256 서명 계산
        var computedSignature = ComputeHmacSha256(paramString, secretKey);

        // 타이밍 어택 방지를 위해 CryptographicOperations.FixedTimeEquals 사용
        var providedBytes = HexStringToBytes(providedSignature);
        var computedBytes = HexStringToBytes(computedSignature);

        if (providedBytes is null || computedBytes is null ||
            !CryptographicOperations.FixedTimeEquals(providedBytes, computedBytes))
        {
            _logger.LogWarning(
                "IronSource HMAC 불일치 — IP: {Ip}, TransactionId: {TxId}",
                context.RemoteIp, transactionId);
            throw new InvalidAdSignatureException("IronSource", "HMAC 서명이 일치하지 않습니다.");
        }

        // userId = 플레이어 ID (IronSource는 게임 서버가 전달한 CustomId로 PlayerId를 받음)
        if (!int.TryParse(userIdStr, out var playerId))
            throw new InvalidAdSignatureException("IronSource", $"userId가 유효한 PlayerId가 아닙니다: {userIdStr}");

        // 타임스탬프 파싱 및 리플레이 공격 방지 (허용 윈도우: ±10분)
        DateTime eventTime;
        if (queryParams.TryGetValue("timestamp", out var tsStr) && long.TryParse(tsStr, out var tsSec))
        {
            eventTime = DateTimeOffset.FromUnixTimeSeconds(tsSec).UtcDateTime;
            var drift = Math.Abs((DateTime.UtcNow - eventTime).TotalMinutes);
            if (drift > 10)
                throw new InvalidAdSignatureException("IronSource", $"타임스탬프가 허용 범위를 벗어났습니다 (drift: {drift:F1}분)");
        }
        else
        {
            eventTime = DateTime.UtcNow;
        }

        return Task.FromResult(new AdCallbackVerified(
            PlayerId: playerId,
            PlacementId: placementId,
            TransactionId: transactionId,
            EventTime: eventTime
        ));
    }

    // signature를 제외한 파라미터를 키 기준 오름차순으로 정렬 후 "&key=value" 형태로 연결
    private static string BuildSignatureString(IReadOnlyDictionary<string, string> queryParams)
    {
        var parts = queryParams
            .Where(kv => !string.Equals(kv.Key, "signature", StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}");

        return string.Join("&", parts);
    }

    // HMAC-SHA256 서명 계산 후 소문자 hex 반환
    private static string ComputeHmacSha256(string message, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    // hex 문자열을 byte 배열로 변환 (유효하지 않은 hex이면 null 반환)
    private static byte[]? HexStringToBytes(string hex)
    {
        try
        {
            return Convert.FromHexString(hex);
        }
        catch
        {
            return null;
        }
    }
}
