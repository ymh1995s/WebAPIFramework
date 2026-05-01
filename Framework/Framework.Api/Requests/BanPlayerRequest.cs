// 플레이어 밴 처리 요청 body 모델 — Controller에서 [FromBody]로 수신
using System.ComponentModel.DataAnnotations;

namespace Framework.Api.Requests;

// Reason은 nullable — 기존 호출자(body에 BannedUntil만 있는 경우)와 하위 호환
// [property:] 타겟 명시 — record positional parameter에서 property에 어트리뷰트 적용 (MVC 모델 검증 동작)
public record BanPlayerRequest(DateTime? BannedUntil, [property: MaxLength(500)] string? Reason = null);
