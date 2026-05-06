using Framework.Api.Constants;
using Framework.Api.Filters;
using Framework.Api.Requests;
using Framework.Application.Common;
using Framework.Application.Features.AdminPlayer;
using Framework.Application.Features.BanLog;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 플레이어 조회/관리 컨트롤러 — 비즈니스 로직은 IAdminPlayerService에 위임
[AdminApiKey]
[ApiController]
[Route("api/admin/players")]
public class AdminPlayersController : ControllerBase
{
    private readonly IAdminPlayerService _adminPlayerService;

    public AdminPlayersController(IAdminPlayerService adminPlayerService)
    {
        _adminPlayerService = adminPlayerService;
    }

    // 전체 플레이어 목록 조회 (페이지네이션) — pageSize 최대 100 클램프
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, PaginationLimits.AdminDefault);
        var result = await _adminPlayerService.GetAllAsync(page, pageSize);
        return Ok(result);
    }

    // DeviceId 또는 닉네임 부분 일치 검색 (Admin 전용) — pageSize 최대 100 클램프
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest("검색어를 입력하세요.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, PaginationLimits.AdminDefault);
        var result = await _adminPlayerService.SearchAsync(keyword, page, pageSize);
        return Ok(result);
    }

    // ID로 플레이어 단건 조회
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var player = await _adminPlayerService.GetByIdAsync(id);
        if (player is null) return NotFound();
        return Ok(player);
    }

    // 플레이어 밴 처리 — body: { bannedUntil: "2026-05-01T00:00:00Z", reason: "..." } 또는 null이면 영구 밴
    [HttpPost("{id}/ban")]
    public async Task<IActionResult> Ban(int id, [FromBody] BanPlayerRequest request)
    {
        // 처리 요청 IP 추출 — BanLog 감사 이력에 기록
        var actorIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _adminPlayerService.BanAsync(id, request.BannedUntil, request.Reason, actorIp);

        return result switch
        {
            BanOperationResult.PlayerNotFound => NotFound(),
            // 이미 밴 상태 — 중복 밴 불가 (409 Conflict)
            BanOperationResult.AlreadyBanned  => Conflict(new MessageResponse("이미 밴 상태인 플레이어입니다.")),
            _ => Ok(new MessageResponse(request.BannedUntil.HasValue
                ? $"{request.BannedUntil:yyyy-MM-dd HH:mm} UTC까지 밴 처리됨"
                : "영구 밴 처리됨"))
        };
    }

    // 플레이어 밴 해제 — body: { reason: "..." } (body 전체 생략 가능, 기존 호출과 호환)
    [HttpPost("{id}/unban")]
    public async Task<IActionResult> Unban(int id, [FromBody] UnbanPlayerRequest? request = null)
    {
        // 처리 요청 IP 추출 — BanLog 감사 이력에 기록
        var actorIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _adminPlayerService.UnbanAsync(id, request?.Reason, actorIp);

        return result switch
        {
            BanOperationResult.PlayerNotFound => NotFound(),
            // 밴 상태가 아님 — 밴해제 불가 (409 Conflict)
            BanOperationResult.NotBanned      => Conflict(new MessageResponse("밴 상태가 아닌 플레이어입니다.")),
            _                                 => Ok(new MessageResponse("밴 해제 완료"))
        };
    }

    // 플레이어 DB 직접 삭제 (DELETE /api/admin/players/{id}) — 소프트 딜리트 미적용 계정 대상
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _adminPlayerService.DeleteAsync(id);
        if (!success) return NotFound();
        return Ok(new MessageResponse("플레이어가 삭제되었습니다."));
    }

    // 플레이어 인앱결제 건수 조회 — 하드삭제 모달 경고 표시용 (GET /api/admin/players/{id}/iap-count)
    [HttpGet("{id}/iap-count")]
    public async Task<IActionResult> GetIapCount(int id)
    {
        var count = await _adminPlayerService.GetIapPurchaseCountAsync(id);
        return Ok(new { Count = count });
    }

    // 플레이어 하드삭제 — 탈퇴 처리(IsDeleted=true)된 계정만 허용 (DELETE /api/admin/players/{id}/hard)
    // IapPurchase 선삭제 → 게임 데이터 정리 → Player 삭제 단일 트랜잭션
    [HttpDelete("{id}/hard")]
    public async Task<IActionResult> HardDelete(int id)
    {
        var result = await _adminPlayerService.HardDeleteAsync(id);

        return result switch
        {
            HardDeleteResult.NotFound     => NotFound(),
            // 탈퇴 처리되지 않은 계정 — 하드삭제 불가 (409 Conflict)
            HardDeleteResult.NotWithdrawn => Conflict(new MessageResponse("탈퇴 처리(소프트 딜리트)된 계정만 하드삭제할 수 있습니다.")),
            _                            => Ok(new MessageResponse("플레이어가 영구 삭제되었습니다."))
        };
    }
}
