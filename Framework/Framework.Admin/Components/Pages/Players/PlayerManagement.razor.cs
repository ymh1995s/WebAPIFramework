using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Players;

/// <summary>
/// 플레이어 관리 페이지 코드-비하인드.
/// 목록 조회, 검색, 밴/밴 해제, 삭제, 게스트 등록 기능을 담당한다.
/// </summary>
public partial class PlayerManagement : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 전체 목록 (페이지네이션) ───────────────────
    private PlayerPagedResult? pagedResult;
    private int currentPage = 1;
    private const int pageSize = 20;
    private bool isLoading = true;

    // ─── DeviceId/닉네임 검색 ───────────────────────
    private string searchKeyword = "";
    private PlayerPagedResult? searchPagedResult;  // 검색 결과 페이지네이션 (DB 레벨)
    private string? searchError;
    private int searchCurrentPage = 1;  // 검색 결과 현재 페이지
    /// <summary>검색 모드 여부 (true이면 검색 결과 패널 표시)</summary>
    private bool isSearchMode = false;

    // ─── 신규 등록 ──────────────────────────────────
    private string newDeviceId = "";
    private string? registerMessage;
    private bool registerSuccess;

    // ─── 삭제 확인 모달 상태 ────────────────────────
    private bool showDeleteModal = false;
    private string deletingPlayerInfo = "";
    private int deletingPlayerId = 0;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SafeExecute(LoadPaged, msg => registerMessage = msg);
            StateHasChanged();
        }
    }

    /// <summary>플레이어 목록 조회 (페이지네이션)</summary>
    private async Task LoadPaged()
    {
        isLoading = true;
        var client = HttpClientFactory.CreateClient("ApiClient");
        pagedResult = await client.GetFromJsonAsync<PlayerPagedResult>(ApiRoutes.AdminPlayers.Paged(currentPage, pageSize));
        isLoading = false;
    }

    private async Task PrevPage()
    {
        if (currentPage > 1) { currentPage--; await LoadPaged(); }
    }

    private async Task NextPage()
    {
        if (pagedResult != null && currentPage < pagedResult.TotalPages) { currentPage++; await LoadPaged(); }
    }

    /// <summary>Enter 키 입력 시 검색 실행</summary>
    private async Task OnSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SearchByKeyword();
    }

    /// <summary>DeviceId 또는 닉네임 부분 일치 검색 (DB 페이지네이션)</summary>
    private async Task SearchByKeyword()
    {
        searchPagedResult = null;
        searchError = null;
        isSearchMode = true;

        if (string.IsNullOrWhiteSpace(searchKeyword))
        {
            // 검색어가 없으면 검색 모드 해제 후 전체 목록 표시
            isSearchMode = false;
            return;
        }

        // 새 검색어 입력 시 첫 페이지부터 시작
        searchCurrentPage = 1;
        await LoadSearch();
    }

    /// <summary>검색 결과 페이지 이동</summary>
    private async Task SearchPrevPage()
    {
        if (searchCurrentPage > 1) { searchCurrentPage--; await LoadSearch(); }
    }

    private async Task SearchNextPage()
    {
        if (searchPagedResult != null && searchCurrentPage < searchPagedResult.TotalPages) { searchCurrentPage++; await LoadSearch(); }
    }

    /// <summary>검색 API 호출 — searchCurrentPage 기준으로 DB 페이지네이션 요청</summary>
    private async Task LoadSearch()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminPlayers.Search(searchKeyword, searchCurrentPage, pageSize));

        if (response.IsSuccessStatusCode)
            searchPagedResult = await response.Content.ReadFromJsonAsync<PlayerPagedResult>();
        else
            searchError = "검색 중 오류가 발생했습니다.";
    }

    /// <summary>플레이어 밴 처리 — bannedUntil이 null이면 영구 밴</summary>
    private async Task BanPlayer(int playerId, DateTime? bannedUntil)
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { BannedUntil = bannedUntil };
        var response = await client.PostAsJsonAsync(ApiRoutes.AdminPlayers.Ban(playerId), payload);
        if (response.IsSuccessStatusCode)
        {
            // 목록 및 검색 결과 갱신
            await LoadPaged();
            if (isSearchMode && !string.IsNullOrWhiteSpace(searchKeyword))
                await LoadSearch();
        }
    }

    /// <summary>플레이어 밴 해제</summary>
    private async Task UnbanPlayer(int playerId)
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.PostAsJsonAsync(ApiRoutes.AdminPlayers.Unban(playerId), new { });
        if (response.IsSuccessStatusCode)
        {
            await LoadPaged();
            if (isSearchMode && !string.IsNullOrWhiteSpace(searchKeyword))
                await LoadSearch();
        }
    }

    /// <summary>삭제 모달 열기 — 대상 플레이어 정보를 상태에 저장</summary>
    private void OpenDeleteModal(PlayerDto p)
    {
        deletingPlayerId = p.Id;
        deletingPlayerInfo = $"ID: {p.Id} / 닉네임: {p.Nickname}";
        showDeleteModal = true;
    }

    /// <summary>삭제 취소 — 모달 닫기 및 상태 초기화</summary>
    private void CancelDelete()
    {
        showDeleteModal = false;
        deletingPlayerId = 0;
        deletingPlayerInfo = "";
    }

    /// <summary>삭제 확인 — API 호출 후 목록 갱신</summary>
    private async Task ConfirmDelete()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.DeleteAsync(ApiRoutes.AdminPlayers.Delete(deletingPlayerId));

        // 모달 닫기 및 상태 초기화
        showDeleteModal = false;
        deletingPlayerId = 0;
        deletingPlayerInfo = "";

        if (response.IsSuccessStatusCode)
        {
            // 삭제 성공 시 전체 목록 및 검색 결과 갱신
            await LoadPaged();
            if (isSearchMode && !string.IsNullOrWhiteSpace(searchKeyword))
                await LoadSearch();
        }
    }

    /// <summary>검색 초기화 — 전체 목록으로 복귀</summary>
    private void ClearSearch()
    {
        searchKeyword = "";
        searchPagedResult = null;
        searchCurrentPage = 1;
        searchError = null;
        isSearchMode = false;
    }

    /// <summary>DeviceId 자동 생성</summary>
    private void GenerateDeviceId()
    {
        newDeviceId = Guid.NewGuid().ToString();
    }

    /// <summary>게스트 등록 — auth/guest 엔드포인트 호출</summary>
    private async Task RegisterGuest()
    {
        registerMessage = null;

        if (string.IsNullOrWhiteSpace(newDeviceId))
        {
            registerMessage = "DeviceId를 입력하거나 자동 생성해주세요.";
            registerSuccess = false;
            return;
        }

        // 한글 포함 여부 검사
        if (newDeviceId.Any(c => c >= '가' && c <= '힣' || c >= 'ᄀ' && c <= 'ᇿ' || c >= '㄰' && c <= '㆏'))
        {
            registerMessage = "DeviceId에 한글은 사용할 수 없습니다. 영문·숫자만 입력해주세요.";
            registerSuccess = false;
            return;
        }

        // 최소 길이 검사
        if (newDeviceId.Length < 8)
        {
            registerMessage = "DeviceId는 최소 8자 이상이어야 합니다.";
            registerSuccess = false;
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { DeviceId = newDeviceId };
        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Guest, payload);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<GuestLoginResponse>();
            var status = result?.IsNewPlayer == true ? "신규 생성" : "기존 플레이어";
            registerMessage = $"{status}됨 (PlayerId: {result?.PlayerId})";
            registerSuccess = true;
            newDeviceId = "";
            await LoadPaged();
        }
        else
        {
            registerMessage = "등록에 실패했습니다.";
            registerSuccess = false;
        }
    }

    // ─── 밴 상태 표시 문자열 — 기간 밴이면 해제 일시 함께 표기 ───
    private string GetBanLabel(PlayerDto p)
    {
        if (!p.IsBanned) return "정상";
        if (p.BannedUntil.HasValue) return $"밴됨 (~{p.BannedUntil.Value.ToLocalTime():MM-dd HH:mm})";
        return "영구밴";
    }

    /// <summary>계정 상태 라벨 — 소프트 딜리트 여부 및 병합 대상 PlayerId 표시</summary>
    private string GetAccountStatusLabel(PlayerDto p)
    {
        if (!p.IsDeleted) return "활성";
        var deletedAt = p.DeletedAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "?";
        if (p.MergedIntoPlayerId.HasValue)
            return $"삭제됨 (→ ID {p.MergedIntoPlayerId}, {deletedAt})";
        return $"삭제됨 ({deletedAt})";
    }

    // ─── 로컬 DTO — IsBanned/BannedUntil + PublicId/소프트 딜리트 정보 포함 ───
    private record PlayerDto(
        int Id,
        Guid PublicId,
        string DeviceId,
        string Nickname,
        string? GoogleId,
        DateTime CreatedAt,
        DateTime LastLoginAt,
        bool IsBanned,
        DateTime? BannedUntil,
        bool IsDeleted,
        DateTime? DeletedAt,
        int? MergedIntoPlayerId);

    private record PlayerPagedResult(List<PlayerDto> Items, int TotalCount, int Page, int PageSize)
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    private record GuestLoginResponse(string AccessToken, string RefreshToken, Guid PlayerId, bool IsNewPlayer);
}
