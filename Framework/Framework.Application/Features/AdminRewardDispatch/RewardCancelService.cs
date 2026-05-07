using Framework.Application.Features.Mail;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.AdminRewardDispatch;

// 보상 지급 취소 서비스 구현체
// Direct 지급(MailId=null)은 취소 불가 — 422 반환
// Mail 지급은 IsCancelled 플래그만 설정하고, 선택 시 안내 우편을 발송함
public class RewardCancelService : IRewardCancelService
{
    private readonly IRewardGrantRepository _grantRepository;
    private readonly IMailService _mailService;
    private readonly ILogger<RewardCancelService> _logger;

    public RewardCancelService(
        IRewardGrantRepository grantRepository,
        IMailService mailService,
        ILogger<RewardCancelService> logger)
    {
        _grantRepository = grantRepository;
        _mailService = mailService;
        _logger = logger;
    }

    // 단건 보상 지급 취소
    // [제약] Direct 지급(MailId=null)은 취소 불가 — 보상이 이미 인벤토리에 즉시 반영되어 있기 때문
    // [동작] IsCancelled 플래그 설정 후 선택적으로 플레이어에게 안내 우편 발송
    public async Task<CancelRewardResult> CancelAsync(int grantId, CancelRewardDto dto, int? adminId)
    {
        // 지급 이력 조회
        var grant = await _grantRepository.GetByIdAsync(grantId);
        if (grant is null)
        {
            _logger.LogWarning("보상 취소 실패 — 이력 없음 (GrantId={GrantId})", grantId);
            return new CancelRewardResult(false, "해당 지급 이력을 찾을 수 없습니다.", NotFound: true);
        }

        // 이미 취소된 경우 — 멱등 처리
        if (grant.IsCancelled)
        {
            _logger.LogInformation("보상 취소 요청 — 이미 취소된 이력 (GrantId={GrantId})", grantId);
            return new CancelRewardResult(false, "이미 취소된 보상입니다.", AlreadyCancelled: true);
        }

        // Direct 지급(MailId=null)은 취소 불가
        // 즉시 인벤토리에 반영된 상태이므로 롤백 수단 없음
        if (!grant.MailId.HasValue)
        {
            _logger.LogWarning("보상 취소 불가 — Direct 지급 건 (GrantId={GrantId})", grantId);
            return new CancelRewardResult(false, "Direct 지급 보상은 취소할 수 없습니다.", IsDirectGrant: true);
        }

        // 플레이어가 이미 우편을 수령한 경우 취소 불가
        // 수령된 보상은 이미 인벤토리에 반영되어 있으므로 우편 만료만으로는 롤백 불가
        if (grant.Mail is { IsClaimed: true })
        {
            _logger.LogWarning("보상 취소 불가 — 이미 수령된 우편 (GrantId={GrantId}, MailId={MailId})",
                grantId, grant.MailId);
            return new CancelRewardResult(false, "이미 수령된 보상은 취소할 수 없습니다.", AlreadyClaimed: true);
        }

        // 취소 플래그 및 메타 정보 설정
        grant.IsCancelled = true;
        grant.CancelledAt = DateTime.UtcNow;
        grant.CancelReason = dto.Reason;
        grant.CancelledByAdminId = adminId;

        // 연결된 우편을 즉시 만료 처리 — 플레이어가 더 이상 수령할 수 없도록 차단
        // ExpiresAt을 현재 시각 -1초로 설정하여 만료 판정 기준을 과거로 이동
        if (grant.Mail is not null)
            grant.Mail.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);

        await _grantRepository.SaveChangesAsync();

        _logger.LogInformation(
            "보상 지급 취소 완료 (GrantId={GrantId}, PlayerId={PlayerId}, AdminId={AdminId}, Reason={Reason})",
            grantId, grant.PlayerId, adminId, dto.Reason);

        // 플레이어 안내 우편 발송 (선택)
        if (dto.SendMailNotification)
        {
            // 안내 우편 제목/본문 — 미입력 시 기본 문구 사용
            var title = !string.IsNullOrWhiteSpace(dto.NotificationMailTitle)
                ? dto.NotificationMailTitle
                : "보상 지급 취소 안내";

            var body = !string.IsNullOrWhiteSpace(dto.NotificationMailBody)
                ? dto.NotificationMailBody
                : $"안녕하세요.\n\n이전에 지급된 보상이 취소되었습니다.\n\n사유: {dto.Reason}\n\n문의 사항이 있으시면 고객센터로 연락해 주세요.";

            try
            {
                // 아이템 없는 순수 텍스트 안내 우편 발송 (30일 유효)
                await _mailService.SendAsync(new SendMailDto(
                    PlayerId: grant.PlayerId,
                    Title: title,
                    Body: body,
                    ItemId: null,
                    ItemCount: 0,
                    ExpiresInDays: 30
                ));

                _logger.LogInformation(
                    "보상 취소 안내 우편 발송 완료 (PlayerId={PlayerId})", grant.PlayerId);
            }
            catch (Exception ex)
            {
                // 안내 우편 발송 실패는 취소 자체를 롤백하지 않음 — 경고 로그만 기록
                _logger.LogWarning(ex,
                    "보상 취소 안내 우편 발송 실패 — 취소는 정상 처리됨 (GrantId={GrantId})", grantId);
            }
        }

        return new CancelRewardResult(true, "보상 지급이 취소되었습니다.");
    }
}
