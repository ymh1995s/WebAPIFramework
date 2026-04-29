using Framework.Domain.Entities;
using Framework.Domain.Enums;

namespace Framework.Domain.Interfaces;

// 인앱결제 상품 저장소 인터페이스
public interface IIapProductRepository
{
    // Store + ProductId 조합으로 활성 상품 조회 (삭제 및 비활성 제외)
    Task<IapProduct?> FindActiveAsync(IapStore store, string productId);

    // ID로 단건 조회 (소프트 딜리트 포함 — 관리 목적)
    Task<IapProduct?> GetByIdAsync(int id);

    // Admin 필터 검색 — 스토어/유형/활성 여부 필터 + 페이지네이션
    Task<(List<IapProduct> Items, int TotalCount)> SearchAsync(
        IapStore? store, IapProductType? productType, bool? isEnabled, int page, int pageSize);

    // 새 상품 추가
    Task AddAsync(IapProduct product);

    // 변경사항 저장
    Task SaveChangesAsync();
}
