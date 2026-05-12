using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Application.Features.Inquiry;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Support;

/// <summary>
/// 소원수리함(문의 관리) 페이지 코드-비하인드.
/// 문의 목록 조회 및 관리자 답변 저장을 담당한다.
/// </summary>
public partial class Inquiries : SafeComponentBase
{
    // 의존성 주입 — ApiHttpClient 래퍼를 통해 camelCase JSON 옵션 일관 적용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;

    // 문의 목록 상태
    private List<InquiryAdminDto> inquiries = [];
    private bool isLoading = true;
    private string? errorMessage;

    // 답변 폼 상태
    private int? replyingId;
    private string replyContent = "";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SafeExecute(LoadInquiries, msg => errorMessage = msg);
            StateHasChanged();
        }
    }

    /// <summary>전체 문의 목록 조회</summary>
    private async Task LoadInquiries()
    {
        isLoading = true;
        errorMessage = null;
        // GetRawAsync로 응답 코드 확인 후 AdminJsonOptions.Default로 역직렬화
        var response = await ApiClient.GetRawAsync(ApiRoutes.AdminInquiries.Collection);

        if (response.IsSuccessStatusCode)
            inquiries = await response.Content.ReadFromJsonAsync<List<InquiryAdminDto>>(AdminJsonOptions.Default) ?? [];
        else
            errorMessage = "문의 목록 조회에 실패했습니다.";

        isLoading = false;
    }

    /// <summary>답변 폼 열기 — 기존 답변이 있으면 편집 필드에 미리 채움</summary>
    private void OpenReply(InquiryAdminDto item)
    {
        replyingId = item.Id;
        replyContent = item.AdminReply ?? "";
    }

    private void CloseReply()
    {
        replyingId = null;
        replyContent = "";
    }

    /// <summary>답변 저장</summary>
    private async Task SubmitReply(int id)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(replyContent)) return;

        // PostAsync — AdminJsonOptions.Default로 직렬화하여 답변 저장
        var response = await ApiClient.PostAsync(
            ApiRoutes.AdminInquiries.Reply(id),
            new { Reply = replyContent });

        if (response.IsSuccessStatusCode)
        {
            CloseReply();
            await LoadInquiries();
        }
        else
        {
            errorMessage = "답변 저장에 실패했습니다.";
        }
    }

}
