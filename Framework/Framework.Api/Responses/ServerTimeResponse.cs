namespace Framework.Api.Responses;

// 서버 시간 동기화 응답 — 클라이언트가 UTC·KST·UnixMs를 한 번에 수신하여 로컬 시계 보정에 활용
public sealed record ServerTimeResponse(
    // 서버 현재 시각 (UTC, 오프셋 포함)
    DateTimeOffset Utc,

    // 한국 표준시 (UTC+9)
    DateTimeOffset Kst,

    // Unix 에포크 기준 밀리초 — 클라이언트 정밀 보정용
    long UnixMs
);
