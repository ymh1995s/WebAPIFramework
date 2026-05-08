namespace Framework.Application.Features.ItemUse;

// 게임 특화 아이템 사용 효과 확장 인터페이스 — 선택적 DI 등록
// IEnumerable<IItemUseEffectExtension>으로 주입받아 등록된 모든 구현체를 순차 실행
// 게임별 특수 효과(버프 부여, 스탯 증가 등)가 필요한 경우에만 구현하여 등록
public interface IItemUseEffectExtension
{
    // 아이템 사용 효과 적용 — 수량 차감 및 보상 지급 완료 후 호출됨
    Task ApplyAsync(ItemUseContext context, CancellationToken ct = default);
}
