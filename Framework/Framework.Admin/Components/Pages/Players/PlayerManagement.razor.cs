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
    private string? deletingExtraInfo;    // 결제 건수 등 부가 경고 정보 — null이면 모달에서 비표시
    private int deletingPlayerId = 0;
    // 하드삭제 여부 — true이면 /hard 엔드포인트, false이면 소프트 딜리트
    private bool _isHardDelete = false;

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

    /// <summary>소프트 딜리트 모달 열기 — 활성 계정 대상</summary>
    private void OpenDeleteModal(PlayerDto p)
    {
        deletingPlayerId    = p.Id;
        deletingPlayerInfo  = $"ID: {p.Id} / 닉네임: {p.Nickname}";
        deletingExtraInfo   = null;
        _isHardDelete       = false;
        showDeleteModal     = true;
    }

    /// <summary>하드삭제 모달 열기 — 탈퇴 계정 대상, IAP 결제 건수를 API로 조회하여 경고 표시</summary>
    private async Task OpenHardDeleteModal(PlayerDto p)
    {
        deletingPlayerId   = p.Id;
        deletingPlayerInfo = $"ID: {p.Id} / 닉네임: {p.Nickname}";
        _isHardDelete      = true;

        // 결제 건수 조회 — 모달에 소실 경고 표시
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminPlayers.IapCount(p.Id));
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<IapCountResponse>();
            deletingExtraInfo = result?.Count > 0
                ? $"주의: 인앱결제 이력 {result.Count}건이 함께 삭제됩니다."
                : null;
        }
        else
        {
            // 건수 조회 실패 시에도 모달은 열고 경고만 표시하지 않음
            deletingExtraInfo = null;
        }

        showDeleteModal = true;
    }

    /// <summary>삭제 취소 — 모달 닫기 및 상태 초기화</summary>
    private void CancelDelete()
    {
        showDeleteModal    = false;
        deletingPlayerId   = 0;
        deletingPlayerInfo = "";
        deletingExtraInfo  = null;
        _isHardDelete      = false;
    }

    /// <summary>삭제 확인 — 하드삭제 여부에 따라 엔드포인트 분기 후 목록 갱신</summary>
    private async Task ConfirmDelete()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");

        // 하드삭제: DELETE /api/admin/players/{id}/hard, 소프트 딜리트: DELETE /api/admin/players/{id}
        var url = _isHardDelete
            ? ApiRoutes.AdminPlayers.HardDelete(deletingPlayerId)
            : ApiRoutes.AdminPlayers.Delete(deletingPlayerId);

        var response = await client.DeleteAsync(url);

        // 모달 닫기 및 상태 초기화
        showDeleteModal    = false;
        deletingPlayerId   = 0;
        deletingPlayerInfo = "";
        deletingExtraInfo  = null;
        _isHardDelete      = false;

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

    // ─── 밴 상태 표시 문자열 — 실효 밴 기준으로 표시, 기간 밴이면 해제 일시 함께 표기 ───
    private string GetBanLabel(PlayerDto p)
    {
        if (!p.IsEffectivelyBanned) return "정상";
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

    // ─── 로컬 DTO — IsBanned/BannedUntil/IsEffectivelyBanned + PublicId/소프트 딜리트 정보 포함 ───
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
        // 실효 밴 여부 — 만료된 기간 밴은 false, 버튼 분기 및 상태 표시에 사용
        bool IsEffectivelyBanned,
        bool IsDeleted,
        DateTime? DeletedAt,
        int? MergedIntoPlayerId);

    private record PlayerPagedResult(List<PlayerDto> Items, int TotalCount, int Page, int PageSize)
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    private record GuestLoginResponse(string AccessToken, string RefreshToken, Guid PlayerId, bool IsNewPlayer);

    // IAP 결제 건수 API 응답 — GET /api/admin/players/{id}/iap-count 응답 역직렬화용
    private record IapCountResponse(int Count);
}
