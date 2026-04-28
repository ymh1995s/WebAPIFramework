using Framework.Application.Features.Inquiry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Player;

// 플레이어 전용 문의 컨트롤러
[Authorize]
[ApiController]
[Route("api/inquiries")]
public class InquiriesController : ControllerBase
{
    private readonly IInquiryService _inquiryService;

    public InquiriesController(IInquiryService inquiryService)
        => _inquiryService = inquiryService;

    // 문의 제출 — JWT에서 PlayerId 추출하여 본인 문의로 저장
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitInquiryDto dto)
    {
        var playerId = int.Parse(User.FindFirst("playerId")!.Value);
        await _inquiryService.SubmitAsync(playerId, dto);
        return Created(string.Empty, null);
    }

    // 내 문의 목록 조회 — 답변 포함
    [HttpGet]
    public async Task<IActionResult> GetMy()
    {
        var playerId = int.Parse(User.FindFirst("playerId")!.Value);
        var result = await _inquiryService.GetMyInquiriesAsync(playerId);
        return Ok(result);
    }
}
