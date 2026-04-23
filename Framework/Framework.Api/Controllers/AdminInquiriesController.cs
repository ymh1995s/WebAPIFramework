using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Framework.Api.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// Admin 전용 문의 관리 컨트롤러
[AdminApiKey]
[ApiController]
[Route("api/admin/inquiries")]
public class AdminInquiriesController : ControllerBase
{
    private readonly IInquiryService _inquiryService;

    public AdminInquiriesController(IInquiryService inquiryService)
        => _inquiryService = inquiryService;

    // 전체 문의 목록 조회
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _inquiryService.GetAllAsync());

    // 문의에 답변 등록
    [HttpPost("{id}/reply")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyInquiryDto dto)
    {
        var success = await _inquiryService.ReplyAsync(id, dto);
        return success ? Ok() : NotFound();
    }
}
