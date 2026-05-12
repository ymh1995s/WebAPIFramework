using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Application.Features.Notice;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Support;

/// <summary>
/// 공지 관리 페이지 코드-비하인드.
/// 공지 생성, 수정, 삭제 기능을 담당한다.
/// </summary>
public partial class Notices : SafeComponentBase
{
    // 의존성 주입 — ApiHttpClient 래퍼를 통해 camelCase JSON 옵션 일관 적용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;

    // 공지 목록 상태
    private List<NoticeAdminDto> notices = [];
    private bool isLoading = true;
    private string? errorMessage;

    // 생성 폼 상태
    private string newContent = "";
    private string? createMessage;

    // 인라인 수정 상태
    private int? editingId;
    private string editContent = "";
    private bool editIsActive;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SafeExecute(LoadNotices, msg => errorMessage = msg);
            StateHasChanged();
        }
    }

    /// <summary>공지 목록 조회</summary>
    private async Task LoadNotices()
    {
        isLoading = true;
        errorMessage = null;
        // GetRawAsync로 응답 코드 확인 후 AdminJsonOptions.Default로 역직렬화
        var response = await ApiClient.GetRawAsync(ApiRoutes.AdminNotices.Collection);

        if (response.IsSuccessStatusCode)
            notices = await response.Content.ReadFromJsonAsync<List<NoticeAdminDto>>(AdminJsonOptions.Default) ?? [];
        else
            errorMessage = "공지 목록 조회에 실패했습니다.";

        isLoading = false;
    }

    /// <summary>공지 생성</summary>
    private async Task CreateNotice()
    {
        createMessage = null;
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(newContent)) return;

        // PostAsync — AdminJsonOptions.Default로 직렬화하여 공지 생성
        var response = await ApiClient.PostAsync(ApiRoutes.AdminNotices.Collection, new { Content = newContent });

        if (response.IsSuccessStatusCode)
        {
            newContent = "";
            createMessage = "공지가 등록되었습니다.";
            await LoadNotices();
        }
        else
        {
            errorMessage = "공지 등록에 실패했습니다.";
        }
    }

    /// <summary>수정 시작 — 현재 값을 편집 필드에 채움</summary>
    private void StartEdit(NoticeAdminDto notice)
    {
        editingId = notice.Id;
        editContent = notice.Content;
        editIsActive = notice.IsActive;
    }

    private void CancelEdit() => editingId = null;

    /// <summary>수정 저장</summary>
    private async Task SaveEdit(NoticeAdminDto notice)
    {
        errorMessage = null;
        // PutAsync — AdminJsonOptions.Default로 직렬화하여 공지 수정 저장
        var response = await ApiClient.PutAsync(
            ApiRoutes.AdminNotices.ById(notice.Id),
            new { Content = editContent, IsActive = editIsActive });

        if (response.IsSuccessStatusCode)
        {
            editingId = null;
            await LoadNotices();
        }
        else
        {
            errorMessage = "공지 수정에 실패했습니다.";
        }
    }

    /// <summary>공지 삭제</summary>
    private async Task DeleteNotice(int id)
    {
        errorMessage = null;
        // DeleteAsync — 공지 삭제
        var response = await ApiClient.DeleteAsync(ApiRoutes.AdminNotices.ById(id));

        if (response.IsSuccessStatusCode)
            await LoadNotices();
        else
            errorMessage = "공지 삭제에 실패했습니다.";
    }

}
