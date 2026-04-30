// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

using System.ComponentModel.DataAnnotations;

namespace Framework.Application.Content.Stage;

// 스테이지 클리어 완료 요청 DTO — 클라이언트가 보내는 클리어 결과 데이터
// [Range] 어트리뷰트가 동작하려면 프로퍼티 방식이어야 함 (positional record parameter에는 적용 불가)
public record StageClearRequestDto
{
    // 이번 플레이 점수 (0 이상)
    [Range(0, int.MaxValue)]
    public int Score { get; init; }

    // 획득 별 수 (0~3)
    [Range(0, 3)]
    public int Stars { get; init; }

    // 클리어 소요 시간 밀리초 (0 이상)
    [Range(0, int.MaxValue)]
    public int ClearTimeMs { get; init; }
}

// 스테이지 클리어 완료 응답 DTO — 지급된 보상 내역 포함
public record StageClearResponseDto(
    // 최초 클리어 여부
    bool IsFirstClear,

    // 누적 클리어 횟수
    int ClearCount,

    // 지급된 Exp
    int ExpGranted,

    // 최초 클리어 보상 지급 결과 메시지 (null이면 미지급)
    string? FirstRewardMessage,

    // 재클리어 보상 지급 결과 메시지 (null이면 미지급)
    string? ReplayRewardMessage
);

// 스테이지 진행 현황 DTO — 플레이어의 클리어 기록 + 스테이지 정보
public record StageProgressDto(
    // 스테이지 ID
    int StageId,

    // 스테이지 코드
    string Code,

    // 스테이지 이름
    string Name,

    // 클리어 여부
    bool IsCleared,

    // 누적 클리어 횟수
    int ClearCount,

    // 최고 점수
    int BestScore,

    // 최고 별 수
    int BestStars,

    // 최단 클리어 시간 (밀리초)
    int BestClearTimeMs,

    // 잠금 여부 — 선행 스테이지 미클리어 시 true
    bool IsLocked,

    // 정렬 순서
    int SortOrder
);

// 스테이지 마스터 목록 조회용 DTO — Admin 및 클라이언트 공용
public record StageDto(
    int Id,
    string Code,
    string Name,
    string? RewardTableCode,
    string? RePlayRewardTableCode,
    int RePlayRewardDecayPercent,
    int ExpReward,
    int? RequiredPrevStageId,
    bool IsActive,
    int SortOrder
);

// Admin 스테이지 생성 요청 DTO
public record CreateStageDto(
    string Code,
    string Name,
    string? RewardTableCode,
    string? RePlayRewardTableCode,
    int RePlayRewardDecayPercent,
    int ExpReward,
    int? RequiredPrevStageId,
    bool IsActive,
    int SortOrder
);

// Admin 스테이지 수정 요청 DTO
public record UpdateStageDto(
    string Name,
    string? RewardTableCode,
    string? RePlayRewardTableCode,
    int RePlayRewardDecayPercent,
    int ExpReward,
    int? RequiredPrevStageId,
    bool IsActive,
    int SortOrder
);
