using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Rewards;

/// <summary>
/// 수동 보상 지급 페이지 코드-비하인드.
/// - 단일 플레이어 + 재화/아이템 있음: POST /api/admin/reward-dispatch/grant (AdminGrant, 멱등성)
/// - 전체 플레이어 (Bulk): POST /api/admin/mails/bulk
/// </summary>
public partial class RewardDispatch : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 대상 모드 ──────────────────────────────────
    // "single" = 특정 플레이어, "bulk" = 전체 플레이어
    private string targetMode = "single";

    // ─── 단일 플레이어 입력 필드 ────────────────────
    private int playerId;
    private string sourceKey = "";

    // 지급 방식: "0"=Direct, "1"=Mail, "2"=Auto (서버 DispatchMode enum 정수값)
    // Bulk 모드에서는 "1"(Mail)로 강제 고정
    private string dispatchMode = "2";

    // 재화 (단일 플레이어 전용)
    private int? gold;
    private int? gems;
    private int? exp;

    // 아이템 목록 (단일/Bulk 공통)
    private List<ItemGrantModel> items = new();

    // ─── 우편 설정 (공통) ───────────────────────────
    private string mailTitle = "";
    private string mailBody = "";
    private int mailExpiresInDays = 30;

    // ─── 결과 상태 ──────────────────────────────────
    private string? resultMessage;
    private bool resultSuccess;
    private GrantResultDetail? resultDetail;

    /// <summary>대상 모드 변경 핸들러 — Bulk 선택 시 지급 방식을 Mail(1)로 강제 설정</summary>
    private void OnTargetModeChanged(string mode)
    {
        targetMode = mode;
        if (mode == "bulk")
        {
            // Bulk는 우편 발송만 지원하므로 Mail 모드로 강제 고정
            dispatchMode = "1";
        }
        else
        {
            // 단일 플레이어로 전환 시 Auto(2)로 복원
            dispatchMode = "2";
        }
        // 모드 전환 시 결과 메시지 초기화
        resultMessage = null;
        resultDetail = null;
    }

    /// <summary>SourceKey 자동 생성 — 운영 티켓 번호 형식</summary>
    private void GenerateSourceKey()
    {
        var guid6 = Guid.NewGuid().ToString("N")[..6];
        sourceKey = $"admin-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guid6}";
    }

    /// <summary>아이템 행 추가</summary>
    private void AddItem()
    {
        items.Add(new ItemGrantModel { ItemId = 0, Quantity = 1 });
    }

    /// <summary>아이템 행 제거</summary>
    private void RemoveItem(ItemGrantModel item)
    {
        items.Remove(item);
    }

    /// <summary>
    /// 지급/발송 실행 — 모드에 따라 적절한 API를 호출한다.
    /// - Bulk: POST /api/admin/mails/bulk
    /// - 단일 + 재화/아이템 있음: POST /api/admin/reward-dispatch/grant
    /// - 단일 + 재화/아이템 없음 (순수 우편): POST /api/admin/mails
    /// </summary>
    private async Task Execute()
    {
        resultMessage = null;
        resultDetail = null;

        if (targetMode == "bulk")
        {
            await ExecuteBulkMail();
        }
        else
        {
            await ExecuteSingle();
        }
    }

    /// <summary>단일 플레이어 지급 처리 — 재화/아이템이 있는 경우에만 실행 가능</summary>
    private async Task ExecuteSingle()
    {
        // 플레이어 ID 유효성 검사
        if (playerId <= 0)
        {
            resultMessage = "유효한 플레이어 ID를 입력해주세요.";
            resultSuccess = false;
            return;
        }

        // 지급할 보상 존재 여부 확인
        var hasContent = (gold > 0) || (gems > 0) || (exp > 0)
                         || items.Any(i => i.ItemId > 0 && i.Quantity > 0);
        if (!hasContent)
        {
            resultMessage = "지급할 보상이 없습니다.";
            resultSuccess = false;
            return;
        }

        await ExecuteSingleGrant();
    }

    /// <summary>단일 플레이어 보상 지급 — POST /api/admin/reward-dispatch/grant</summary>
    private async Task ExecuteSingleGrant()
    {
        // PlayerId 존재 여부 사전 확인 — API 실패 전 즉시 피드백
        var client = HttpClientFactory.CreateClient("ApiClient");
        var checkRes = await client.GetAsync(ApiRoutes.AdminPlayers.ById(playerId));
        if (checkRes.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            resultSuccess = false;
            resultMessage = "존재하지 않는 플레이어 ID입니다.";
            return;
        }

        // SourceKey 유효성 검사
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            resultMessage = "SourceKey를 입력해주세요.";
            resultSuccess = false;
            return;
        }

        if (!int.TryParse(dispatchMode, out var modeInt))
            modeInt = 2;

        var payload = new
        {
            PlayerId = playerId,
            SourceKey = sourceKey,
            Gold = gold,
            Gems = gems,
            Exp = exp,
            Items = items.Where(i => i.ItemId > 0 && i.Quantity > 0)
                .Select(i => new { i.ItemId, i.Quantity }).ToList(),
            Mode = modeInt,
            MailTitle = string.IsNullOrWhiteSpace(mailTitle) ? null : mailTitle,
            MailBody = string.IsNullOrWhiteSpace(mailBody) ? null : mailBody,
            MailExpiresInDays = mailExpiresInDays
        };

        // 위에서 생성한 client 재사용 (PlayerId 검증 시 이미 생성됨)
        var response = await client.PostAsJsonAsync(ApiRoutes.AdminRewardDispatch.Grant, payload);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<GrantResponse>();
            resultSuccess = true;
            resultMessage = result?.Message ?? "지급 완료";
            if (result is not null)
                resultDetail = new GrantResultDetail(result.UsedMode, result.MailId);

            // 성공 시 입력 초기화
            sourceKey = "";
            gold = null;
            gems = null;
            exp = null;
            items = new();
        }
        else
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            resultSuccess = false;
            resultMessage = error?.Message ?? $"지급 실패: {response.StatusCode}";
        }
    }

    /// <summary>전체 플레이어 일괄 우편 발송 — POST /api/admin/mails/bulk</summary>
    private async Task ExecuteBulkMail()
    {
        // 제목 필수 검사
        if (string.IsNullOrWhiteSpace(mailTitle))
        {
            resultMessage = "제목을 입력해주세요.";
            resultSuccess = false;
            return;
        }

        // Bulk는 아이템을 첫 번째 항목만 사용 (API 스펙에 따라 단일 아이템)
        var firstItem = items.FirstOrDefault(i => i.ItemId > 0 && i.Quantity > 0);

        var payload = new
        {
            Title = mailTitle,
            Body = mailBody,
            ItemId = firstItem?.ItemId,
            ItemCount = firstItem?.Quantity ?? 0,
            ExpiresInDays = mailExpiresInDays
        };

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.PostAsJsonAsync(ApiRoutes.AdminMails.Bulk, payload);

        if (response.IsSuccessStatusCode)
        {
            resultSuccess = true;
            resultMessage = "전체 플레이어에게 우편이 발송되었습니다.";
            mailTitle = "";
            mailBody = "";
            items = new();
        }
        else
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            resultSuccess = false;
            resultMessage = error?.Message ?? $"발송 실패: {response.StatusCode}";
        }
    }

    // ─── 내부 모델 ──────────────────────────────────

    // 아이템 지급 편집 모델
    private class ItemGrantModel
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    // API 응답 모델
    private record GrantResponse(string Message, string UsedMode, int? MailId, bool AlreadyGranted = false);
    private record ErrorResponse(string Message);
    private record GrantResultDetail(string UsedMode, int? MailId);
}
