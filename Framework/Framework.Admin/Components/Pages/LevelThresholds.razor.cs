using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages;

/// <summary>
/// 레벨 임계값 관리 페이지 코드-비하인드.
/// 레벨별 누적 경험치 기준 조회, 편집, 저장 기능을 제공한다.
/// </summary>
public partial class LevelThresholds : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 상태 ─────────────────────────────────────────
    // 현재 편집 중인 레벨 목록
    private List<LevelRow> rows = new();
    private bool isLoading;
    private bool isSaving;
    private string? errorMessage;
    private string? successMessage;

    // 컴포넌트 초기화 시 서버에서 레벨 테이블 로드
    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    // 서버에서 레벨 임계값 목록 로드
    private async Task LoadAsync()
    {
        isLoading = true;
        errorMessage = null;
        successMessage = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminLevelThresholds.Collection);

        if (response.IsSuccessStatusCode)
        {
            var items = await response.Content.ReadFromJsonAsync<List<LevelThresholdDto>>();
            rows = (items ?? new List<LevelThresholdDto>())
                .Select(d => new LevelRow { Level = d.Level, RequiredExp = d.RequiredExp })
                .ToList();
        }
        else
        {
            errorMessage = $"레벨 테이블 로드 실패: {response.StatusCode}";
        }

        isLoading = false;
    }

    // 서버에서 다시 불러오기 (변경 사항 취소)
    private async Task ReloadFromServer()
    {
        await LoadAsync();
    }

    // 레벨 추가 — 마지막 레벨 + 1 행 추가
    private void AddLevel()
    {
        var nextLevel = rows.Count > 0 ? rows.Max(r => r.Level) + 1 : 1;
        // 이전 레벨 RequiredExp보다 1 이상 크게 기본값 설정
        var prevExp = rows.Count > 0 ? rows.Max(r => r.RequiredExp) : 0;
        rows.Add(new LevelRow { Level = nextLevel, RequiredExp = prevExp + 100 });
    }

    // 레벨 삭제 — Level 1은 삭제 불가
    private void RemoveLevel(LevelRow row)
    {
        if (row.Level == 1) return;
        rows.Remove(row);

        // 삭제 후 레벨 번호 재정렬
        RenumberLevels();
    }

    // RequiredExp 입력값 변경 처리
    private void OnExpChanged(LevelRow row, ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var value))
            row.RequiredExp = value;
    }

    // 레벨 번호 재정렬 — 행 삭제 후 1부터 연속되도록 재부여
    private void RenumberLevels()
    {
        for (var i = 0; i < rows.Count; i++)
            rows[i].Level = i + 1;
    }

    // 저장 — PUT /api/admin/level-thresholds
    private async Task Save()
    {
        isSaving = true;
        errorMessage = null;
        successMessage = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new
        {
            Items = rows.Select(r => new { Level = r.Level, RequiredExp = r.RequiredExp }).ToList()
        };

        var response = await client.PutAsJsonAsync(ApiRoutes.AdminLevelThresholds.Collection, payload);

        if (response.IsSuccessStatusCode)
        {
            successMessage = "레벨 테이블이 저장되었습니다.";
            // 저장 성공 후 서버 데이터로 재로드 (레벨 번호 확인)
            await LoadAsync();
        }
        else
        {
            // 서버 에러 메시지 파싱
            try
            {
                var err = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                errorMessage = $"저장 실패: {err?.Message ?? response.StatusCode.ToString()}";
            }
            catch
            {
                errorMessage = $"저장 실패: {response.StatusCode}";
            }
        }

        isSaving = false;
    }

    // ─── 내부 모델 ──────────────────────────────────

    // 편집 가능한 레벨 행 모델
    private class LevelRow
    {
        public int Level { get; set; }
        public int RequiredExp { get; set; }
    }

    // 레벨 임계값 DTO — API 응답 역직렬화용
    private record LevelThresholdDto(int Level, int RequiredExp);

    // 에러 응답 구조
    private record ErrorResponse(string Message);
}
