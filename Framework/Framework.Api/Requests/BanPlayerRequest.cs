// 플레이어 밴 처리 요청 body 모델 — Controller에서 [FromBody]로 수신
namespace Framework.Api.Requests;

public record BanPlayerRequest(DateTime? BannedUntil);
