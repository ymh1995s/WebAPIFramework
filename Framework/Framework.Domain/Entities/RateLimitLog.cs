namespace Framework.Domain.Entities;

/// <summary>
/// Rate Limit 초과(429) 발생 시 기록되는 로그 엔티티
/// </summary>
public class RateLimitLog
{
    /// <summary>기본 키</summary>
    public int Id { get; set; }

    /// <summary>요청을 시도한 IP 주소</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>요청 경로 (예: /api/auth/login)</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>적용된 정책 이름 (auth / global)</summary>
    public string Policy { get; set; } = string.Empty;

    /// <summary>발생 시각 (UTC)</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>요청자 플레이어 ID — JWT 인증된 요청이면 클레임에서 추출, 비인증이면 null</summary>
    public int? PlayerId { get; set; }

    /// <summary>User-Agent 헤더 (최대 256자) — 봇 탐지 보조용</summary>
    public string? UserAgent { get; set; }
}
