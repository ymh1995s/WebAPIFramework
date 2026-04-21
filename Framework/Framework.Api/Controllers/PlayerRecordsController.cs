using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// 플레이어 기록 API 컨트롤러
[ApiController]
[Route("api/[controller]")]
public class PlayerRecordsController : ControllerBase
{
    private readonly IPlayerRecordService _service;

    public PlayerRecordsController(IPlayerRecordService service)
    {
        _service = service;
    }

    // 전체 기록 조회 (페이지네이션)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(page, pageSize));

    // ID로 단건 조회
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    // 새 기록 저장
    [HttpPost]
    public async Task<IActionResult> Create(CreatePlayerRecordDto dto)
    {
        await _service.CreateAsync(dto);
        return Created();
    }
}
