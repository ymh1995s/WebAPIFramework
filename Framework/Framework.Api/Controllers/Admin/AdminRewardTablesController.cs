using Framework.Api.Filters;
using Framework.Application.Features.RewardTable;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 보상 테이블 CRUD 컨트롤러
// [POST /] 생성, [GET /] 목록, [GET /{id}] 단건, [PUT /{id}] 수정, [DELETE /{id}] 소프트삭제, [PUT /{id}/entries] 항목 일괄 교체
[AdminApiKey]
[ApiController]
[Route("api/admin/reward-tables")]
public class AdminRewardTablesController : ControllerBase
{
    private readonly IRewardTableService _service;

    public AdminRewardTablesController(IRewardTableService service)
    {
        _service = service;
    }

    // 보상 테이블 목록 조회 (sourceType, code 필터 + 페이지네이션)
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] RewardTableFilterDto filter)
    {
        var result = await _service.SearchAsync(filter);
        return Ok(result);
    }

    // 보상 테이블 단건 조회 (Entries 포함)
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // 보상 테이블 생성 — UNIQUE(SourceType, Code) 위반 시 409 Conflict, Code 형식 위반 시 400
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRewardTableDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            if (result is null)
                return Conflict(new { message = "동일한 SourceType + Code 조합의 보상 테이블이 이미 존재합니다." });

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            // Code 형식 또는 길이 검증 실패
            return BadRequest(new { message = ex.Message });
        }
    }

    // 보상 테이블 설명 수정 (SourceType/Code 불변)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRewardTableDto dto)
    {
        var success = await _service.UpdateAsync(id, dto);
        if (!success) return NotFound();
        return Ok(new { message = "보상 테이블 설명이 수정되었습니다." });
    }

    // 보상 테이블 소프트 삭제 (IsDeleted = true)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> SoftDelete(int id)
    {
        var success = await _service.SoftDeleteAsync(id);
        if (!success) return NotFound();
        return Ok(new { message = "보상 테이블이 삭제되었습니다." });
    }

    // 보상 테이블 항목 일괄 교체 — 기존 항목 전부 삭제 후 신규 항목 삽입
    [HttpPut("{id:int}/entries")]
    public async Task<IActionResult> ReplaceEntries(int id, [FromBody] List<EntryUpsertDto> entries)
    {
        var success = await _service.ReplaceEntriesAsync(id, entries);
        if (!success) return NotFound();
        return Ok(new { message = $"{entries.Count}개 항목으로 교체되었습니다." });
    }
}
