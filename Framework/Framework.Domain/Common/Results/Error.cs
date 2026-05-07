namespace Framework.Domain.Common.Results;

// 작업 실패 사유 — 코드(Unity 클라이언트 분기용 안정 식별자)와 메시지 페어
public sealed record Error(string Code, string Message)
{
    // 성공 시 사용하는 빈 오류 — IsSuccess 확인 후 접근할 것
    public static readonly Error None        = new("",                  "");
    public static readonly Error NullValue   = new("ERR_NULL",          "값이 비어 있습니다.");
    public static readonly Error NotFound    = new("ERR_NOT_FOUND",     "대상을 찾을 수 없습니다.");
    public static readonly Error Conflict    = new("ERR_CONFLICT",      "상태 충돌이 발생했습니다.");
    public static readonly Error Unauthorized = new("ERR_UNAUTHORIZED", "인증되지 않았습니다.");
}
