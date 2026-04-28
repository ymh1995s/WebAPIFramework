using Framework.Api.Filters;
using Framework.Api.Requests;
using Framework.Application.Features.AdminPlayer;
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

    // 전체 플레이어 목록 조회 (페이지네이션)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _adminPlayerService.GetAllAsync(page, pageSize);
        return Ok(result);
    }

    // DeviceId 또는 닉네임 부분 일치 검색 (Admin 전용)
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest("검색어를 입력하세요.");

        var items = await _adminPlayerService.SearchAsync(keyword);
        return Ok(items);
    }

    // ID로 플레이어 단건 조회
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var player = await _adminPlayerService.GetByIdAsync(id);
        if (player is null) return NotFound();
        return Ok(player);
    }

    // 플레이어 밴 처리 — body: { bannedUntil: "2026-05-01T00:00:00Z" } 또는 null이면 영구 밴
    [HttpPost("{id}/ban")]
    public async Task<IActionResult> Ban(int id, [FromBody] BanPlayerRequest request)
    {
        var success = await _adminPlayerService.BanAsync(id, request.BannedUntil);
        if (!success) return NotFound();

        var message = request.BannedUntil.HasValue
            ? $"{request.BannedUntil:yyyy-MM-dd HH:mm} UTC까지 밴 처리됨"
            : "영구 밴 처리됨";
        return Ok(new { message });
    }

    // 플레이어 밴 해제
    [HttpPost("{id}/unban")]
    public async Task<IActionResult> Unban(int id)
    {
        var success = await _adminPlayerService.UnbanAsync(id);
        if (!success) return NotFound();
        return Ok(new { message = "밴 해제 완료" });
    }

    // 플레이어 영구 삭제 (Hard Delete) — DB에서 완전히 제거, 복구 불가
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _adminPlayerService.DeleteAsync(id);
        if (!success) return NotFound();
        return Ok(new { message = "플레이어가 영구 삭제되었습니다." });
    }
}
