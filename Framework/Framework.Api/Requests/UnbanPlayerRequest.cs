// 밴 해제 요청 body — Reason 선택 입력
// body 자체가 null인 기존 호출(body 없음)과도 호환 — Controller에서 nullable로 수신
using System.ComponentModel.DataAnnotations;

namespace Framework.Api.Requests;

public record UnbanPlayerRequest([MaxLength(500)] string? Reason = null);
