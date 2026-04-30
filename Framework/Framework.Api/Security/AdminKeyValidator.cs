using System.Security.Cryptography;
using System.Text;

namespace Framework.Api.Security;

// X-Admin-Key 헤더 검증 인터페이스 — 점검 미들웨어와 [AdminApiKey] 필터에서 공유
public interface IAdminKeyValidator
{
    bool IsValid(string? providedKey);
}

// 타이밍 공격에 안전한 Admin API Key 검증기
// CryptographicOperations.FixedTimeEquals 사용으로 비교 시간 일정화
// Singleton 등록 — 시작 시점 1회 인코딩 후 불변
public sealed class AdminKeyValidator : IAdminKeyValidator
{
    private readonly byte[]? _expectedKeyBytes;

    public AdminKeyValidator(IConfiguration config)
    {
        // 시작 시점에 1회만 변환 — 매 요청마다 인코딩 비용 회피
        var expected = config["Admin:ApiKey"];
        _expectedKeyBytes = string.IsNullOrEmpty(expected)
            ? null
            : Encoding.UTF8.GetBytes(expected);
    }

    public bool IsValid(string? providedKey)
    {
        // 빈 키 또는 미설정 키는 항상 거부 — 빈 문자열로 통과되는 사고 방지
        if (string.IsNullOrEmpty(providedKey) || _expectedKeyBytes is null || _expectedKeyBytes.Length == 0)
            return false;

        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        // 길이 사전 체크 — 운영 키 길이는 고정이므로 길이 누설 위험 무시 가능
        if (providedBytes.Length != _expectedKeyBytes.Length)
            return false;

        // 타이밍 공격 방어 — 모든 바이트를 끝까지 비교하여 응답 시간 일정화
        return CryptographicOperations.FixedTimeEquals(providedBytes, _expectedKeyBytes);
    }
}
