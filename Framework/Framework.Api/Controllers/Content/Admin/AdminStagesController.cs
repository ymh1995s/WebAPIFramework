// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

using Framework.Api.Filters;
using Framework.Application.Common;
using Framework.Application.Content.Stage;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Content.Admin;

// Admin 전용 스테이지 마스터 CRUD 컨트롤러
// - GET    /api/admin/stages         : 목록 조회 (페이지네이션)
// - GET    /api/admin/stages/{id}    : 단건 조회
// - POST   /api/admin/stages         : 생성
// - PUT    /api/admin/stages/{id}    : 수정
[AdminApiKey]
[ApiController]
[Route("api/admin/stages")]
public class AdminStagesController : ControllerBase
{
    private readonly IStageClearService _service;

    public AdminStagesController(IStageClearService service)
    {
        _service = service;
    }

    // 스테이지 목록 조회 — 키워드/페이지네이션 지원
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // pageSize 범위 제한 — 비정상적으로 큰 값 요청 시 DB 부하 방지 (M-37)
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _service.SearchAsync(keyword, page, pageSize);
        return Ok(new PagedResultDto<StageDto>(items, total, page, pageSize));
    }

    // 스테이지 단건 조회
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // 스테이지 생성 — Code UNIQUE 위반 시 409 Conflict
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStageDto dto)
    {
        try
        {
            var result = await _service.CreateStageAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (Exception ex) when (ex.Message.Contains("unique") ||
                                    ex.InnerException?.Message.Contains("23505") == true)
        {
            // Code UNIQUE 위반
            return Conflict(new MessageResponse("동일한 코드의 스테이지가 이미 존재합니다."));
        }
    }

    // 스테이지 수정
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStageDto dto)
    {
        var success = await _service.UpdateStageAsync(id, dto);
        if (!success) return NotFound();
        return Ok(new MessageResponse("스테이지가 수정되었습니다."));
    }
}
