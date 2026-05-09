namespace Framework.Application.Features.SystemConfig;

// RemoteConfig 항목 단건 DTO — Admin API 응답에서 prefix 포함 원본 키를 반환할 때 사용
public record ClientConfigDto(string Key, string Value);
