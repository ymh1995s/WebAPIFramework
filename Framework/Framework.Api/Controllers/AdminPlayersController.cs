using Framework.Api.Filters;
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
                p.LastLoginAt
            })
            .ToList();

        return Ok(new { Items = items, TotalCount = total, Page = page, PageSize = pageSize });
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
            player.LastLoginAt
        });
    }
}
