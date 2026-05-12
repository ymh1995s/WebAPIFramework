using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Application.Features.Auth;
using Framework.Application.Features.Inquiry;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Support;

/// <summary>
/// 문의 테스트 페이지 코드-비하인드.
/// 플레이어 역할로 게스트 로그인 후 문의 제출/조회를 테스트한다.
/// SubmitInquiry/LoadMyInquiries는 플레이어 Bearer 토큰이 필요하여 IHttpClientFactory 직접 사용.
/// InquiryTest는 SafeComponentBase를 상속하지 않으므로 ComponentBase를 직접 사용한다.
/// </summary>
public partial class InquiryTest : Microsoft.AspNetCore.Components.ComponentBase
{
    // 의존성 주입 — GuestLogin에는 ApiHttpClient, 토큰 인증 요청에는 IHttpClientFactory 직접 사용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;
    // 플레이어 Bearer 토큰이 필요한 SendAsync 요청에만 사용 (SubmitInquiry, LoadMyInquiries)
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // 로그인 상태
    private string deviceId = "";
    private string? accessToken;
    private Guid loggedInPlayerId;
    private bool isLoggedIn;
    private string? loginError;

    // 문의 제출 상태
    private string inquiryContent = "";
    private string? submitMessage;
    private string? submitError;

    // 문의 목록 상태
    private List<InquiryDto> myInquiries = [];
    private string? listError;

    /// <summary>게스트 로그인 — DeviceId로 JWT 발급</summary>
    private async Task GuestLogin()
    {
        loginError = null;
        // PostAsync — AdminJsonOptions.Default로 직렬화하여 게스트 로그인 요청
        var response = await ApiClient.PostAsync(ApiRoutes.Auth.Guest, new { DeviceId = deviceId });

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<TokenResponseDto>(AdminJsonOptions.Default);
            if (result is not null)
            {
                accessToken = result.AccessToken;
                loggedInPlayerId = result.PlayerId;
                isLoggedIn = true;
            }
        }
        else
        {
            loginError = $"로그인 실패: {response.StatusCode}";
        }
    }

    private void Logout()
    {
        accessToken = null;
        isLoggedIn = false;
        loggedInPlayerId = Guid.Empty;
        myInquiries = [];
    }

    /// <summary>문의 제출 — Bearer 토큰으로 플레이어 인증</summary>
    private async Task SubmitInquiry()
    {
        submitMessage = null;
        submitError = null;
        if (string.IsNullOrWhiteSpace(inquiryContent)) return;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Inquiries.Collection);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { Content = inquiryContent });

        var response = await client.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            inquiryContent = "";
            submitMessage = "문의가 접수되었습니다.";
        }
        else
        {
            submitError = $"제출 실패: {response.StatusCode}";
        }
    }

    /// <summary>내 문의 목록 조회</summary>
    private async Task LoadMyInquiries()
    {
        listError = null;
        var client = HttpClientFactory.CreateClient("ApiClient");
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Inquiries.Collection);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);

        if (response.IsSuccessStatusCode)
            myInquiries = await response.Content.ReadFromJsonAsync<List<InquiryDto>>(AdminJsonOptions.Default) ?? [];
        else
            listError = $"조회 실패: {response.StatusCode}";
    }

}
