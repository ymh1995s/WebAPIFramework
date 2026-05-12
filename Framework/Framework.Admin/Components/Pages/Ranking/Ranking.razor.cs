using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Application.Features.Ranking;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Ranking;

/// <summary>
/// 랭킹 조회 페이지 코드-비하인드.
/// 상위 N명의 랭킹 데이터를 조회하여 표시한다.
/// </summary>
public partial class Ranking : SafeComponentBase
{
    // 의존성 주입 — ApiHttpClient 래퍼를 통해 camelCase JSON 옵션 일관 적용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;

    // 조회할 랭킹 인원 수 (기본 100명)
    private int count = 100;
    private bool isLoading;
    // 서버 RankingDto 직접 사용 — PlayerId가 Guid임을 명확히 (int 로컬 record 제거)
    private List<RankingDto>? rankings;
    private string? errorMessage;

    /// <summary>상위 N명 랭킹 조회</summary>
    private async Task LoadRanking()
    {
        isLoading = true;
        errorMessage = null;

        // GetRawAsync로 응답 코드 확인 후 AdminJsonOptions.Default로 역직렬화
        var response = await ApiClient.GetRawAsync(ApiRoutes.AdminRanking.Top(count));

        if (response.IsSuccessStatusCode)
            rankings = await response.Content.ReadFromJsonAsync<List<RankingDto>>(AdminJsonOptions.Default);
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

}
