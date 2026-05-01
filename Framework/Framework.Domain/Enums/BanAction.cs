namespace Framework.Domain.Enums;

// 밴 처리 액션 종류 — BanLog.Action 컬럼에 저장
public enum BanAction
{
    // 밴 처리 (기간 밴 또는 영구 밴)
    Ban = 1,

    // 밴 해제
    Unban = 2,
}
