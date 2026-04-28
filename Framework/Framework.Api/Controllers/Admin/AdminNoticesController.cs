using Framework.Api.Filters;
using Framework.Application.Features.Notice;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 공지 관리 컨트롤러
[AdminApiKey]
[ApiController]
[Route("api/admin/notices")]
public class AdminNoticesController : ControllerBase
{
    private readonly INoticeService _noticeService;

    public AdminNoticesController(INoticeService noticeService)
    {
        _noticeService = noticeService;
    }

    // 전체 공지 목록 조회
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _noticeService.GetAllAsync());

    // 공지 생성
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNoticeDto dto)
    {
        var result = await _noticeService.CreateAsync(dto);
        return Created(string.Empty, result);
    }

    // 공지 수정
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateNoticeDto dto)
    {
        var success = await _noticeService.UpdateAsync(id, dto);
        return success ? Ok() : NotFound();
    }

    // 공지 삭제
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _noticeService.DeleteAsync(id);
        return success ? Ok() : NotFound();
    }
}
