using Framework.Api.Constants;
using Framework.Api.Extensions;
using Framework.Application.Features.Inquiry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// 플레이어 전용 문의 컨트롤러
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Game)]
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
        var playerId = User.GetPlayerIdRequired();
        await _inquiryService.SubmitAsync(playerId, dto);
        return Created(string.Empty, null);
    }

    // 내 문의 목록 조회 — 답변 포함
    [HttpGet]
    public async Task<IActionResult> GetMy()
    {
        var playerId = User.GetPlayerIdRequired();
        var result = await _inquiryService.GetMyInquiriesAsync(playerId);
        return Ok(result);
    }
}
