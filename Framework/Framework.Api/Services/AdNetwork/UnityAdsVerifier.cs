using System.Security.Cryptography;
using System.Text;
using Framework.Application.Features.AdReward;
using Framework.Domain.Enums;

namespace Framework.Api.Services.AdNetwork;

// Unity Ads SSV(Server Side Verification) 검증기
// Unity Ads 공식 문서: https://docs.unity.com/ads/en-us/manual/ServerSideVerification
// 서명 방식: HMAC-SHA256(rawQueryString, secretKey) → hex 비교
public class UnityAdsVerifier : IAdNetworkVerifier
{
    // 이 검증기가 담당하는 광고 네트워크
    public AdNetworkType Network => AdNetworkType.UnityAds;

    private readonly IConfiguration _config;
    private readonly ILogger<UnityAdsVerifier> _logger;

    public UnityAdsVerifier(IConfiguration config, ILogger<UnityAdsVerifier> logger)
    {
        _config = config;
        _logger = logger;
    }

    // Unity Ads SSV 콜백 검증
    // Unity Ads는 쿼리 파라미터로 hmac(서명), sid(플레이어ID), productid(PlacementId), oid(거래ID), ts(타임스탬프) 전달
    public Task<AdCallbackVerified> VerifyAsync(AdCallbackContext context)
    {
        // appsettings.json의 AdNetworks:UnityAds:SecretKey 에서 시크릿 키 로드
        var secretKey = _config["AdNetworks:UnityAds:SecretKey"]
            ?? throw new InvalidOperationException("Unity Ads SecretKey가 설정되지 않았습니다.");

        var queryParams = context.QueryParams;

        // 필수 파라미터 존재 확인
        if (!queryParams.TryGetValue("hmac", out var providedHmac) || string.IsNullOrEmpty(providedHmac))
            throw new InvalidAdSignatureException("UnityAds", "hmac 파라미터가 없습니다.");

        if (!queryParams.TryGetValue("sid", out var sidStr) || string.IsNullOrEmpty(sidStr))
            throw new InvalidAdSignatureException("UnityAds", "sid 파라미터가 없습니다.");

        if (!queryParams.TryGetValue("oid", out var transactionId) || string.IsNullOrEmpty(transactionId))
            throw new InvalidAdSignatureException("UnityAds", "oid 파라미터가 없습니다.");

        if (!queryParams.TryGetValue("productid", out var placementId) || string.IsNullOrEmpty(placementId))
            throw new InvalidAdSignatureException("UnityAds", "productid 파라미터가 없습니다.");

        // hmac을 제외한 쿼리 스트링으로 서명 재계산
        // Unity Ads는 hmac 파라미터 자체를 제외하고 나머지 쿼리 스트링에 서명
        var queryWithoutHmac = BuildQueryWithoutHmac(context.RawQueryString);

        // HMAC-SHA256 서명 계산
        var computedHmac = ComputeHmacSha256(queryWithoutHmac, secretKey);

        // 타이밍 어택 방지를 위해 CryptographicOperations.FixedTimeEquals 사용
        var providedBytes = HexStringToBytes(providedHmac);
        var computedBytes = HexStringToBytes(computedHmac);

        if (providedBytes is null || computedBytes is null ||
            !CryptographicOperations.FixedTimeEquals(providedBytes, computedBytes))
        {
            _logger.LogWarning(
                "Unity Ads HMAC 불일치 — IP: {Ip}, TransactionId: {TxId}",
                context.RemoteIp, transactionId);
            throw new InvalidAdSignatureException("UnityAds", "HMAC 서명이 일치하지 않습니다.");
        }

        // sid = 플레이어 ID (Unity Ads는 게임 서버가 전달한 CustomData로 PlayerId를 받음)
        if (!int.TryParse(sidStr, out var playerId))
            throw new InvalidAdSignatureException("UnityAds", $"sid가 유효한 PlayerId가 아닙니다: {sidStr}");

        // 타임스탬프 파싱 및 리플레이 공격 방지 (허용 윈도우: ±10분)
        DateTime eventTime;
        if (queryParams.TryGetValue("ts", out var tsStr) && long.TryParse(tsStr, out var tsMs))
        {
            eventTime = DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime;
            var drift = Math.Abs((DateTime.UtcNow - eventTime).TotalMinutes);
            if (drift > 10)
                throw new InvalidAdSignatureException("UnityAds", $"타임스탬프가 허용 범위를 벗어났습니다 (drift: {drift:F1}분)");
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

    // hmac 파라미터를 제외한 쿼리 스트링 재구성 — split & filter 방식으로 안전하게 제거
    private static string BuildQueryWithoutHmac(string rawQuery)
    {
        if (string.IsNullOrEmpty(rawQuery))
            return string.Empty;

        var query = rawQuery.TrimStart('?');

        // '&'로 분리 후 key가 "hmac"인 쌍만 제거하고 재조합
        var filtered = query.Split('&')
            .Where(pair => !pair.StartsWith("hmac=", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return string.Join("&", filtered);
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
