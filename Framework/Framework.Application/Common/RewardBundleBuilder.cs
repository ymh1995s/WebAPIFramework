using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;

namespace Framework.Application.Common;

// RewardTable 항목으로 RewardBundle을 구성하는 공용 빌더 — AdReward/IAP 등 여러 서비스에서 공유
// Weight가 있는 항목은 가중치 확률 추첨, 없으면 전체 고정 지급
public static class RewardBundleBuilder
{
    // RewardTable ID로 RewardBundle 구성 — tableRepo는 호출부에서 주입
    // rewardTableId가 null이거나 테이블/항목이 없으면 빈 번들 반환
    public static async Task<RewardBundle> BuildAsync(IRewardTableRepository tableRepo, int? rewardTableId)
    {
        if (rewardTableId is null)
            return new RewardBundle();

        // ID로 RewardTable + Entries 조회
        var table = await tableRepo.GetByIdWithEntriesAsync(rewardTableId.Value);
        if (table is null || table.IsDeleted)
            return new RewardBundle();

        var entries = table.Entries.ToList();
        if (entries.Count == 0)
            return new RewardBundle();

        // Weight가 있는 항목이 있으면 확률 추첨, 없으면 전체 고정 지급
        bool hasWeight = entries.Any(e => e.Weight.HasValue);

        if (hasWeight)
        {
            // 가중치 기반 확률 추첨 — 하나의 항목만 선택
            var totalWeight = entries.Sum(e => e.Weight ?? 0);
            if (totalWeight <= 0)
                return new RewardBundle();

            var roll = Random.Shared.Next(totalWeight);
            var cumulative = 0;
            foreach (var entry in entries)
            {
                cumulative += entry.Weight ?? 0;
                if (roll < cumulative)
                {
                    return new RewardBundle(Items: new[]
                    {
                        new RewardItem(entry.ItemId, entry.Count)
                    });
                }
            }
        }
        else
        {
            // 전체 고정 지급 — Weight 없는 모든 항목 지급
            var items = entries
                .Select(e => new RewardItem(e.ItemId, e.Count))
                .ToArray();
            return new RewardBundle(Items: items);
        }

        return new RewardBundle();
    }
}
