using System.Text.RegularExpressions;
using Framework.Application.Common;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.RewardTable;

// 보상 테이블 Admin 관리 서비스 구현체
public class RewardTableService : IRewardTableService
{
    private readonly IRewardTableRepository _tableRepo;
    private readonly IItemRepository _itemRepo;
    private readonly ILogger<RewardTableService> _logger;

    public RewardTableService(
        IRewardTableRepository tableRepo,
        IItemRepository itemRepo,
        ILogger<RewardTableService> logger)
    {
        _tableRepo = tableRepo;
        _itemRepo = itemRepo;
        _logger = logger;
    }

    // 보상 테이블 목록 조회 (필터 + 페이지네이션)
    public async Task<PagedResultDto<RewardTableDto>> SearchAsync(RewardTableFilterDto filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var (items, total) = await _tableRepo.SearchAsync(
            filter.SourceType, filter.Code, page, pageSize);

        var dtos = items.Select(t => new RewardTableDto(
            t.Id,
            t.SourceType,
            t.Code,
            t.Description,
            t.IsDeleted,
            t.Entries.Count
        )).ToList();

        return new PagedResultDto<RewardTableDto>(dtos, total, page, pageSize);
    }

    // ID로 단건 조회 (Entries 포함)
    public async Task<RewardTableDetailDto?> GetByIdAsync(int id)
    {
        var table = await _tableRepo.GetByIdWithEntriesAsync(id);
        if (table is null) return null;

        // 아이템 이름 조회 캐시
        var itemIds = table.Entries.Select(e => e.ItemId).Distinct().ToList();
        var itemNameMap = new Dictionary<int, string>();
        foreach (var itemId in itemIds)
        {
            var item = await _itemRepo.GetByIdAsync(itemId);
            itemNameMap[itemId] = item?.Name ?? "(삭제된 아이템)";
        }

        var entries = table.Entries.Select(e => new RewardTableEntryDto(
            e.Id,
            e.ItemId,
            itemNameMap.TryGetValue(e.ItemId, out var name) ? name : "",
            e.Count,
            e.Weight
        )).ToList();

        return new RewardTableDetailDto(
            table.Id,
            table.SourceType,
            table.Code,
            table.Description,
            table.IsDeleted,
            entries
        );
    }

    // Code 유효성 검증용 정규식 — 소문자, 숫자, 언더스코어, 콜론만 허용
    private static readonly Regex CodePattern = new(@"^[a-z0-9_:]+$", RegexOptions.Compiled);

    // Code 최대 길이
    private const int CodeMaxLength = 50;

    // 보상 테이블 생성 — UNIQUE(SourceType, Code) 위반 시 null 반환
    // Code 형식 위반(정규식 or 길이 초과) 시 ArgumentException 발생
    public async Task<RewardTableDetailDto?> CreateAsync(CreateRewardTableDto dto)
    {
        var trimmedCode = dto.Code?.Trim() ?? "";

        // Code 필수값 검증
        if (string.IsNullOrEmpty(trimmedCode))
            throw new ArgumentException("Code는 필수 입력입니다.");

        // Code 길이 검증
        if (trimmedCode.Length > CodeMaxLength)
            throw new ArgumentException($"Code는 최대 {CodeMaxLength}자까지 입력 가능합니다. (현재: {trimmedCode.Length}자)");

        // Code 형식 검증 — 소문자, 숫자, _, : 만 허용
        if (!CodePattern.IsMatch(trimmedCode))
            throw new ArgumentException("Code는 소문자, 숫자, 언더스코어(_), 콜론(:)만 허용됩니다. (예: stage_1, match_win_ranked)");

        var table = new Domain.Entities.RewardTable
        {
            SourceType = dto.SourceType,
            Code = trimmedCode,
            Description = dto.Description.Trim(),
            IsDeleted = false
        };

        _logger.LogDebug("보상 테이블 생성 시도 — SourceType: {SourceType}, Code: {Code}", dto.SourceType, trimmedCode);
        await _tableRepo.AddAsync(table);

        try
        {
            await _tableRepo.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // UNIQUE 위반 — 동일 SourceType + Code 이미 존재
            _logger.LogWarning(
                "보상 테이블 생성 실패 — UNIQUE 위반 (SourceType: {SourceType}, Code: {Code})",
                dto.SourceType, dto.Code);
            return null;
        }

        return new RewardTableDetailDto(
            table.Id,
            table.SourceType,
            table.Code,
            table.Description,
            table.IsDeleted,
            new List<RewardTableEntryDto>()
        );
    }

    // 보상 테이블 설명 수정 (SourceType/Code 불변)
    public async Task<bool> UpdateAsync(int id, UpdateRewardTableDto dto)
    {
        var table = await _tableRepo.GetByIdWithEntriesAsync(id);
        if (table is null || table.IsDeleted) return false;

        table.Description = dto.Description.Trim();
        await _tableRepo.SaveChangesAsync();
        return true;
    }

    // 소프트 삭제 — IsDeleted = true
    public async Task<bool> SoftDeleteAsync(int id)
    {
        var table = await _tableRepo.GetByIdWithEntriesAsync(id);
        if (table is null) return false;

        table.IsDeleted = true;
        await _tableRepo.SaveChangesAsync();

        _logger.LogInformation("보상 테이블 소프트 삭제 — Id: {Id}, Code: {Code}", id, table.Code);
        return true;
    }

    // Entries 일괄 교체 — 기존 항목 전체 삭제 후 신규 항목 삽입
    public async Task<bool> ReplaceEntriesAsync(int id, List<EntryUpsertDto> entries)
    {
        var table = await _tableRepo.GetByIdWithEntriesAsync(id);
        if (table is null || table.IsDeleted) return false;

        // 기존 Entries 모두 제거 (EF Core 변경 추적 활용)
        table.Entries.Clear();

        // 신규 항목 삽입
        foreach (var e in entries)
        {
            table.Entries.Add(new RewardTableEntry
            {
                RewardTableId = id,
                ItemId = e.ItemId,
                Count = e.Count,
                Weight = e.Weight
            });
        }

        await _tableRepo.SaveChangesAsync();
        return true;
    }

    // PostgreSQL UNIQUE 제약 위반 여부 확인
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
           || (ex.InnerException?.GetType().Name == "PostgresException" &&
               (ex.InnerException?.Message.Contains("unique") == true ||
                ex.InnerException?.Message.Contains("duplicate") == true));
}
