using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;

namespace Framework.Admin.Components.Pages.Players;

/// <summary>
/// 랭킹 조회 페이지 코드-비하인드.
/// 상위 N명의 랭킹 데이터를 조회하여 표시한다.
/// </summary>
public partial class Ranking : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // 조회할 랭킹 인원 수 (기본 100명)
    private int count = 100;
    private bool isLoading;
    private List<RankingDto>? rankings;
    private string? errorMessage;

    /// <summary>상위 N명 랭킹 조회</summary>
    private async Task LoadRanking()
    {
        isLoading = true;
        errorMessage = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminRanking.Top(count));

        if (response.IsSuccessStatusCode)
            rankings = await response.Content.ReadFromJsonAsync<List<RankingDto>>();
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    // API 응답 매핑용 로컬 DTO
    private record RankingDto(int Rank, int PlayerId, string Nickname, int BestScore);
}
