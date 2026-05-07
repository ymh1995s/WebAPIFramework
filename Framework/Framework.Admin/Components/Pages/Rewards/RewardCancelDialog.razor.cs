using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Rewards;

/// <summary>
/// 보상 지급 취소 다이얼로그 코드-비하인드.
/// 취소 사유(필수)와 안내 우편 발송 여부(선택)를 입력받아 API를 호출한다.
/// </summary>
public partial class RewardCancelDialog
{
    // ─── 외부 파라미터 ──────────────────────────────
    /// <summary>취소할 RewardGrant ID</summary>
    [Parameter] public int GrantId { get; set; }

    /// <summary>대상 플레이어 ID (표시용)</summary>
    [Parameter] public int PlayerId { get; set; }

    /// <summary>취소 완료 후 부모 컴포넌트에서 목록 새로고침을 위한 콜백</summary>
    [Parameter] public EventCallback OnConfirmed { get; set; }

    /// <summary>다이얼로그 닫기 콜백</summary>
    [Parameter] public EventCallback OnClosed { get; set; }

    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 입력 상태 ──────────────────────────────────
    // 취소 사유 — 필수 입력
    private string reason = "";

    // 안내 우편 발송 여부
    private bool sendMailNotification;

    // 안내 우편 제목 (미입력 시 서버 기본값 사용)
    private string? notificationMailTitle;

    // 안내 우편 본문 (미입력 시 서버 기본값 사용)
    private string? notificationMailBody;

    // ─── UI 상태 ────────────────────────────────────
    private bool isProcessing;
    private string? errorMessage;

    /// <summary>취소 확인 — 유효성 검증 후 API 호출</summary>
    private async Task Confirm()
    {
        // 취소 사유 필수 검증
        if (string.IsNullOrWhiteSpace(reason))
        {
            errorMessage = "취소 사유를 입력해 주세요.";
            return;
        }

        isProcessing = true;
        errorMessage = null;

        try
        {
            var client = HttpClientFactory.CreateClient("ApiClient");

            // 취소 요청 DTO 구성
            var requestBody = new
            {
                Reason = reason.Trim(),
                SendMailNotification = sendMailNotification,
                NotificationMailTitle = string.IsNullOrWhiteSpace(notificationMailTitle)
                    ? null
                    : notificationMailTitle.Trim(),
                NotificationMailBody = string.IsNullOrWhiteSpace(notificationMailBody)
                    ? null
                    : notificationMailBody.Trim()
            };

            var response = await client.PostAsJsonAsync(
                ApiRoutes.AdminRewardDispatch.Cancel(GrantId), requestBody);

            if (response.IsSuccessStatusCode)
            {
                // 취소 성공 — 부모 컴포넌트에 완료 알림
                await OnConfirmed.InvokeAsync();
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // 이미 취소된 상태
                errorMessage = "이미 취소된 보상입니다.";
            }
            else if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                // Direct 지급 취소 불가 (UI에서 이미 필터하지만 방어적 처리)
                errorMessage = "Direct 지급 보상은 취소할 수 없습니다.";
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                errorMessage = "해당 지급 이력을 찾을 수 없습니다.";
            }
            else
            {
                errorMessage = $"취소 처리 실패: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"요청 중 오류가 발생했습니다: {ex.Message}";
        }
        finally
        {
            isProcessing = false;
        }
    }
}
