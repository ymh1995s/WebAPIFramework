using Framework.Application.Common;
using Framework.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainAdPolicy = Framework.Domain.Entities.AdPolicy;

namespace Framework.Application.Features.AdPolicy;

// 광고 정책 Admin 관리 서비스 구현체
public class AdPolicyService : IAdPolicyService
{
    private readonly IAdPolicyRepository _policyRepo;
    private readonly ILogger<AdPolicyService> _logger;

    public AdPolicyService(
        IAdPolicyRepository policyRepo,
        ILogger<AdPolicyService> logger)
    {
        _policyRepo = policyRepo;
        _logger = logger;
    }

    // 광고 정책 목록 조회 (필터 + 페이지네이션)
    public async Task<PagedResultDto<AdPolicyDto>> SearchAsync(AdPolicyFilterDto filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var (items, total) = await _policyRepo.SearchAsync(filter.Network, page, pageSize);

        var dtos = items.Select(ToDto).ToList();
        return new PagedResultDto<AdPolicyDto>(dtos, total, page, pageSize);
    }

    // ID로 광고 정책 단건 조회
    public async Task<AdPolicyDto?> GetByIdAsync(int id)
    {
        var policy = await _policyRepo.GetByIdAsync(id);
        return policy is null ? null : ToDto(policy);
    }

    // 광고 정책 생성 — UNIQUE(Network, PlacementId) 위반 시 null 반환
    public async Task<AdPolicyDto?> CreateAsync(CreateAdPolicyDto dto)
    {
        var policy = new DomainAdPolicy
        {
            Network = dto.Network,
            PlacementId = dto.PlacementId.Trim(),
            PlacementType = dto.PlacementType,
            RewardTableId = dto.RewardTableId,
            DailyLimit = dto.DailyLimit,
            IsEnabled = dto.IsEnabled,
            Description = dto.Description.Trim(),
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _logger.LogDebug(
            "광고 정책 생성 시도 — Network: {Network}, PlacementId: {PlacementId}",
            dto.Network, dto.PlacementId);

        await _policyRepo.AddAsync(policy);

        try
        {
            await _policyRepo.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // UNIQUE 위반 — 동일 Network + PlacementId 이미 존재 (IsDeleted=false 기준)
            _logger.LogWarning(
                "광고 정책 생성 실패 — UNIQUE 위반 (Network: {Network}, PlacementId: {PlacementId})",
                dto.Network, dto.PlacementId);
            return null;
        }

        return ToDto(policy);
    }

    // 광고 정책 수정 (Network/PlacementId 불변)
    public async Task<bool> UpdateAsync(int id, UpdateAdPolicyDto dto)
    {
        var policy = await _policyRepo.GetByIdAsync(id);
        if (policy is null || policy.IsDeleted) return false;

        policy.RewardTableId = dto.RewardTableId;
        policy.DailyLimit = dto.DailyLimit;
        policy.IsEnabled = dto.IsEnabled;
        policy.Description = dto.Description.Trim();
        policy.UpdatedAt = DateTime.UtcNow;

        await _policyRepo.SaveChangesAsync();
        return true;
    }

    // 소프트 삭제 — IsDeleted = true
    public async Task<bool> SoftDeleteAsync(int id)
    {
        var policy = await _policyRepo.GetByIdAsync(id);
        if (policy is null) return false;

        policy.IsDeleted = true;
        policy.UpdatedAt = DateTime.UtcNow;
        await _policyRepo.SaveChangesAsync();

        _logger.LogInformation(
            "광고 정책 소프트 삭제 — Id: {Id}, Network: {Network}, PlacementId: {PlacementId}",
            id, policy.Network, policy.PlacementId);
        return true;
    }

    // 엔티티 → DTO 변환 헬퍼
    private static AdPolicyDto ToDto(DomainAdPolicy p) => new(
        p.Id,
        p.Network,
        p.PlacementId,
        p.PlacementType,
        p.RewardTableId,
        p.DailyLimit,
        p.IsEnabled,
        p.Description,
        p.IsDeleted,
        p.CreatedAt,
        p.UpdatedAt
    );

}
