namespace Framework.Api.Responses;

// RemoteConfig 클라이언트 응답 — "client." 접두사를 제거한 키-값 사전을 래핑
// 예: { "values": { "feature_flag": "true", "max_level": "100" } }
public sealed record RemoteConfigResponse(IDictionary<string, string> Values);
