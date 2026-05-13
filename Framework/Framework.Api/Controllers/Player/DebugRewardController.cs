#if DEBUG

using Framework.Api.Extensions;
using Framework.Api.Requests;
using Framework.Application.Features.Reward;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Player;

// 디버그 보상 지급 컨트롤러 — 개발/QA 환경 전용, 릴리즈 빌드에서 제외됨
// POST /api/debug/grant — 대상 플레이어에게 아이템·Exp 즉시 지급
[Authorize]
[ApiController]
[Route("api/debug")]
public class DebugRewardController : ControllerBase
{
    private readonly IRewardDispatcher _rewardDispatcher;
    private readonly IItemRepository _itemRepository;
    private readonly ILogger<DebugRewardController> _logger;

    public DebugRewardController(
        IRewardDispatcher rewardDispatcher,
        IItemRepository itemRepository,
        ILogger<DebugRewardController> logger)
    {
        _rewardDispatcher = rewardDispatcher;
        _itemRepository = itemRepository;
        _logger = logger;
    }

    // 디버그 보상 직접 지급 — 개발/QA 전용
    // [주의] PlayerId 오버라이드 가능하므로 DEBUG 빌드에서만 노출
    [HttpPost("grant")]
    public async Task<IActionResult> Grant([FromBody] DebugGrantRequest request)
    {
        // JWT 클레임 또는 요청 바디의 PlayerId 결정 (바디 우선)
        var targetPlayerId = request.PlayerId ?? User.GetPlayerIdRequired();

        // 빈 페이로드 검증 — Exp와 Items 모두 없으면 지급 불가
        var hasItems = request.Items is { Count: > 0 };
        if (request.Exp == 0 && !hasItems)
            return BadRequest(new { message = "지급할 내용이 없습니다." });

        // ItemId 존재성 배치 검증 — 미존재 ID가 있으면 400 반환
        if (hasItems)
        {
            var requestedIds = request.Items!.Select(i => i.ItemId).Distinct().ToList();
            var existingItems = await _itemRepository.GetByIdsAsync(requestedIds);
            var existingIds = existingItems.Select(i => i.Id).ToHashSet();

            var missingIds = requestedIds.Where(id => !existingIds.Contains(id)).ToList();
            if (missingIds.Count > 0)
                return BadRequest(new { message = $"존재하지 않는 아이템 ID: {string.Join(", ", missingIds)}" });
        }

        // RewardBundle 조립 — 아이템 목록과 Exp 합산
        var bundleItems = hasItems
            ? request.Items!.Select(i => new RewardItem(i.ItemId, i.Quantity)).ToList()
            : null;
        var bundle = new RewardBundle(Exp: request.Exp, Items: bundleItems);

        // SourceKey 결정 — null/공백이면 UUID 자동 생성, 지정 시 "debug:{sourceKey}" 접두사 추가
        var sourceKey = string.IsNullOrWhiteSpace(request.SourceKey)
            ? $"debug:{Guid.NewGuid():N}"
            : $"debug:{request.SourceKey}";

        // 보상 지급 요청 조립 — AdminGrant 타입, System 행위자, Direct 모드
        var grantRequest = new GrantRewardRequest(
            PlayerId: targetPlayerId,
            SourceType: RewardSourceType.AdminGrant,
            SourceKey: sourceKey,
            Bundle: bundle,
            Mode: DispatchMode.Direct,
            ActorType: AuditActorType.System,
            ActorId: null
        );

        // 디버그 지급 경고 로그 — 운영 혼입 추적용
        _logger.LogWarning(
            "DEBUG GRANT — PlayerId={PlayerId}, Items={ItemCount}, Exp={Exp}, SourceKey={SourceKey}",
            targetPlayerId, request.Items?.Count ?? 0, request.Exp, sourceKey);

        var result = await _rewardDispatcher.GrantAsync(grantRequest);

        // 결과 분기 처리
        if (result.IsNotFound)
            return NotFound(new { message = "플레이어를 찾을 수 없습니다." });

        if (result.Success)
            return Ok(new { message = "지급이 완료되었습니다.", sourceKey });

        return BadRequest(new { message = result.Message });
    }
}

#endif
