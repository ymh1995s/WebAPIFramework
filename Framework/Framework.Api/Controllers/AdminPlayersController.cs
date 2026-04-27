using Framework.Api.Filters;
using Framework.Api.Requests;
using Framework.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// Admin 전용 플레이어 조회 컨트롤러
[AdminApiKey]
[ApiController]
[Route("api/admin/players")]
public class AdminPlayersController : ControllerBase
{
    private readonly IPlayerRepository _playerRepository;

    public AdminPlayersController(IPlayerRepository playerRepository)
    {
        _playerRepository = playerRepository;
    }

    // 전체 플레이어 목록 조회 (페이지네이션)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var all = await _playerRepository.GetAllAsync();
        var total = all.Count;
        var items = all
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.DeviceId,
                p.Nickname,
                p.GoogleId,
                p.CreatedAt,
                p.LastLoginAt,
                p.IsBanned,
                p.BannedUntil
            })
            .ToList();

        return Ok(new { Items = items, TotalCount = total, Page = page, PageSize = pageSize });
    }

    // DeviceId 또는 닉네임 부분 일치 검색 (Admin 전용)
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest("검색어를 입력하세요.");

        var players = await _playerRepository.SearchByKeywordAsync(keyword);
        var items = players
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.DeviceId,
                p.Nickname,
                p.GoogleId,
                p.CreatedAt,
                p.LastLoginAt,
                p.IsBanned,
                p.BannedUntil
            })
            .ToList();

        return Ok(items);
    }

    // ID로 플레이어 단건 조회
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return NotFound();

        return Ok(new
        {
            player.Id,
            player.DeviceId,
            player.Nickname,
            player.GoogleId,
            player.CreatedAt,
            player.LastLoginAt,
            player.IsBanned,
            player.BannedUntil
        });
    }

    // 플레이어 밴 처리 — body: { bannedUntil: "2026-05-01T00:00:00Z" } 또는 null이면 영구 밴
    [HttpPost("{id}/ban")]
    public async Task<IActionResult> Ban(int id, [FromBody] BanPlayerRequest request)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return NotFound();

        await _playerRepository.BanAsync(id, request.BannedUntil);
        return Ok(new { message = request.BannedUntil.HasValue ? $"{request.BannedUntil:yyyy-MM-dd HH:mm} UTC까지 밴 처리됨" : "영구 밴 처리됨" });
    }

    // 플레이어 밴 해제
    [HttpPost("{id}/unban")]
    public async Task<IActionResult> Unban(int id)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return NotFound();

        await _playerRepository.UnbanAsync(id);
        return Ok(new { message = "밴 해제 완료" });
    }

    // 밴 요청 DTO — BannedUntil이 null이면 영구 밴, 값이 있으면 기간 밴
}
