namespace Framework.Application.Features.Exp;

// 레벨 테이블 Admin 서비스 인터페이스 — 조회 및 일괄 교체 기능 정의
public interface ILevelTableAdminService
{
    // 전체 레벨 임계값 목록 조회 (레벨 오름차순)
    Task<List<LevelThresholdDto>> GetAllAsync();

    // 레벨 테이블 전체를 새 목록으로 교체
    Task ReplaceAllAsync(List<LevelThresholdDto> items);
}
