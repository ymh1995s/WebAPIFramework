namespace Framework.Admin.Constants;

/// <summary>
/// Admin Blazor에서 호출하는 모든 API 경로를 중앙 관리하는 상수 클래스.
///
/// [왜 이 파일이 필요한가]
/// 경로를 각 razor 파일에 직접 문자열로 쓰면(하드코딩) 두 가지 문제가 생긴다.
///   1. 오타 — 컴파일 타임에 잡히지 않아 런타임에서야 404로 발견된다.
///   2. 산탄총 수술 — 경로 하나가 바뀌면 모든 razor 파일을 직접 찾아 수정해야 한다.
/// 이 파일에 경로를 모아두면 변경 시 이 파일 한 곳만 수정하면 된다.
///
/// [구조 규칙]
/// - 정적 경로(파라미터 없음): const string
/// - 동적 경로(파라미터 있음): static string 메서드로 정의
/// - 컨트롤러별로 중첩 클래스를 나눠 어느 컨트롤러의 경로인지 명확하게 구분
/// </summary>
public static class ApiRoutes
{
    // ── 인증 (AuthController: Route = "auth") ──────────────────────────────
    public static class Auth
    {
        /// <summary>게스트 로그인 — DeviceId 기반 토큰 발급</summary>
        public const string Guest = "auth/guest";
    }

    // ── 플레이어 Admin (AdminPlayersController: Route = "api/admin/players") ──
    public static class AdminPlayers
    {
        private const string Base = "api/admin/players";

        /// <summary>전체 플레이어 목록 페이지네이션 조회</summary>
        public static string Paged(int page, int pageSize) => $"{Base}?page={page}&pageSize={pageSize}";

        /// <summary>ID로 플레이어 단건 조회</summary>
        public static string ById(int id) => $"{Base}/{id}";

        /// <summary>DeviceId 또는 닉네임 부분 일치 검색</summary>
        public static string Search(string keyword) => $"{Base}/search?keyword={Uri.EscapeDataString(keyword)}";

        /// <summary>플레이어 밴 처리 (POST) — body: { bannedUntil: DateTime? }, null이면 영구 밴</summary>
        public static string Ban(int id) => $"{Base}/{id}/ban";

        /// <summary>플레이어 밴 해제 (POST)</summary>
        public static string Unban(int id) => $"{Base}/{id}/unban";

        /// <summary>플레이어 영구 삭제 (DELETE)</summary>
        public static string Delete(int id) => $"{Base}/{id}";
    }

    // ── 랭킹 (RankingController: Route = "api/ranking") ───────────────────
    public static class Ranking
    {
        /// <summary>내 순위 조회 (게임 클라이언트 전용)</summary>
        public const string Me = "api/ranking/me";
    }

    // ── 랭킹 Admin (AdminRankingController: Route = "api/admin/ranking") ──
    public static class AdminRanking
    {
        /// <summary>상위 N명 랭킹 조회 (Admin 전용)</summary>
        public static string Top(int count) => $"api/admin/ranking/top?count={count}";
    }

    // ── 인벤토리 Admin (AdminPlayerItemsController: Route = "api/admin/players/{playerId}/items") ──
    public static class Players
    {
        /// <summary>특정 플레이어의 보유 아이템 목록 조회 (Admin 전용)</summary>
        public static string Items(int playerId) => $"api/admin/players/{playerId}/items";
    }

    // ── 아이템 마스터 Admin (AdminItemsController: Route = "api/admin/items") ──
    public static class AdminItems
    {
        private const string Base = "api/admin/items";

        /// <summary>아이템 목록 조회 (GET) / 아이템 추가 (POST)</summary>
        public const string Collection = Base;

        /// <summary>아이템 수정 (PUT) / 소프트 삭제 (DELETE)</summary>
        public static string ById(int id) => $"{Base}/{id}";

        /// <summary>아이템 보유 유저 수 조회 — 삭제 전 경고 팝업용</summary>
        public static string Holders(int id) => $"{Base}/{id}/holders";
    }

    // ── 우편 Admin (AdminMailsController: Route = "api/admin/mails") ───────
    public static class AdminMails
    {
        /// <summary>특정 플레이어에게 단건 우편 발송 (POST)</summary>
        public const string Single = "api/admin/mails";

        /// <summary>전체 플레이어에게 일괄 우편 발송 (POST)</summary>
        public const string Bulk = "api/admin/mails/bulk";
    }

    // ── Rate Limit 로그 Admin (AdminRateLimitLogsController) ─────────────
    public static class AdminRateLimitLogs
    {
        /// <summary>Rate Limit 초과 로그 목록 조회 (GET)</summary>
        public const string Collection = "api/admin/rate-limit-logs";
    }

    // ── 보안 통합 타임라인 Admin (AdminSecurityController) ────────────────
    public static class AdminSecurity
    {
        private const string Base = "api/admin/security";

        /// <summary>Rate Limit 정책 현재 설정값 조회 (GET)</summary>
        public const string RateLimitConfig = $"{Base}/rate-limit-config";

        /// <summary>통합 보안 타임라인 조회 — 파라미터: from, to, playerId, ip</summary>
        public static string Timeline(DateTime? from, DateTime? to, int? playerId, string? ip)
        {
            var parts = new List<string>();
            if (from.HasValue)     parts.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
            if (to.HasValue)       parts.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
            if (playerId.HasValue) parts.Add($"playerId={playerId.Value}");
            if (!string.IsNullOrEmpty(ip)) parts.Add($"ip={Uri.EscapeDataString(ip)}");
            return parts.Count > 0 ? $"{Base}/timeline?{string.Join("&", parts)}" : $"{Base}/timeline";
        }
    }

    // ── 감사 로그 Admin (AdminAuditLogsController: Route = "api/admin/audit-logs") ──
    public static class AdminAuditLogs
    {
        private const string Base = "api/admin/audit-logs";

        /// <summary>필터/페이지네이션 기반 검색 (GET) — 지원 파라미터: playerId, itemId, from, to, isAnomaly, page, pageSize</summary>
        public static string Search(int? playerId, int? itemId, DateTime? from, DateTime? to, bool? isAnomaly, int page, int pageSize)
        {
            var parts = new List<string>();
            if (playerId.HasValue) parts.Add($"playerId={playerId.Value}");
            if (itemId.HasValue) parts.Add($"itemId={itemId.Value}");
            if (from.HasValue) parts.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
            if (to.HasValue) parts.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
            if (isAnomaly.HasValue) parts.Add($"isAnomaly={isAnomaly.Value.ToString().ToLower()}");
            parts.Add($"page={page}");
            parts.Add($"pageSize={pageSize}");
            return $"{Base}?{string.Join("&", parts)}";
        }
    }

    // ── 일일 보상 슬롯 Admin (AdminDailyRewardSlotsController) ──────────────
    public static class AdminDailyRewardSlots
    {
        private const string Base = "api/admin/daily-rewards/slots";

        /// <summary>슬롯 전체 28개 Day 조회 (GET) — slot: "current" 또는 "next"</summary>
        public static string Slot(string slot) => $"{Base}/{slot}";

        /// <summary>특정 슬롯의 특정 Day 보상 수정 (PUT)</summary>
        public static string SlotDay(string slot, int day) => $"{Base}/{slot}/days/{day}";
    }

    // ── 시스템 설정 Admin (SystemConfigController: Route = "api/admin/systemconfig") ──
    public static class SystemConfig
    {
        private const string Base = "api/admin/systemconfig";

        /// <summary>수동 점검 모드 on/off 조회(GET) / 변경(PUT)</summary>
        public const string MaintenanceMode = $"{Base}/maintenance-mode";

        /// <summary>점검 예약 시각 조회(GET) / 설정(PUT)</summary>
        public const string MaintenanceSchedule = $"{Base}/maintenance-schedule";

        /// <summary>수동+예약 포함 현재 실제 점검 여부 조회 (GET)</summary>
        public const string MaintenanceStatus = $"{Base}/maintenance-status";

        /// <summary>버전 설정 조회(GET) / 저장(PUT)</summary>
        public const string Version = $"{Base}/version";

        /// <summary>일일 보상 하루 기준 시각 조회(GET) / 저장(PUT) — KST 시/분</summary>
        public const string DailyRewardDayBoundary = $"{Base}/daily-reward-day-boundary";

        /// <summary>월 28회 초과 시 기본 보상 설정 조회(GET) / 저장(PUT)</summary>
        public const string DailyRewardDefault = $"{Base}/daily-reward-default";
    }

    // ── 공지 Admin (AdminNoticesController: Route = "api/admin/notices") ────
    public static class AdminNotices
    {
        private const string Base = "api/admin/notices";

        /// <summary>전체 공지 조회 (GET) / 공지 생성 (POST)</summary>
        public const string Collection = Base;

        /// <summary>공지 수정 (PUT) / 삭제 (DELETE)</summary>
        public static string ById(int id) => $"{Base}/{id}";
    }

    // ── 공지 (NoticesController: Route = "api/notices") ──────────────────
    public static class Notices
    {
        /// <summary>최신 활성 공지 1개 조회 (GET)</summary>
        public const string Latest = "api/notices/latest";
    }

    // ── 매치메이킹 (MatchMakingController: Route = "api/matchmaking") ──────
    public static class Matchmaking
    {
        private const string Base = "api/matchmaking";

        /// <summary>매칭 대기열 참가 (POST)</summary>
        public const string Join = Base;

        /// <summary>매칭 대기열 취소 (DELETE)</summary>
        public static string Cancel(string userId) => $"{Base}/{userId}";
    }

    // ── 문의 (InquiriesController: Route = "api/inquiries") ─────────────
    public static class Inquiries
    {
        private const string Base = "api/inquiries";

        /// <summary>문의 제출 (POST) / 내 문의 목록 (GET)</summary>
        public const string Collection = Base;
    }

    // ── 문의 Admin (AdminInquiriesController: Route = "api/admin/inquiries") ──
    public static class AdminInquiries
    {
        private const string Base = "api/admin/inquiries";

        /// <summary>전체 문의 목록 조회 (GET)</summary>
        public const string Collection = Base;

        /// <summary>문의 답변 등록 (POST)</summary>
        public static string Reply(int id) => $"{Base}/{id}/reply";
    }

    // ── 보상 테이블 Admin (AdminRewardTablesController: Route = "api/admin/reward-tables") ──
    public static class AdminRewardTables
    {
        private const string Base = "api/admin/reward-tables";

        /// <summary>목록 조회 (GET) / 생성 (POST)</summary>
        public const string Collection = Base;

        /// <summary>단건 조회 (GET) / 수정 (PUT) / 소프트 삭제 (DELETE)</summary>
        public static string ById(int id) => $"{Base}/{id}";

        /// <summary>항목 일괄 교체 (PUT)</summary>
        public static string Entries(int id) => $"{Base}/{id}/entries";

        /// <summary>필터 + 페이지네이션 검색</summary>
        public static string Search(int? sourceType, string? code, int page, int pageSize)
        {
            var parts = new List<string>();
            if (sourceType.HasValue) parts.Add($"sourceType={sourceType.Value}");
            if (!string.IsNullOrEmpty(code)) parts.Add($"code={Uri.EscapeDataString(code)}");
            parts.Add($"page={page}");
            parts.Add($"pageSize={pageSize}");
            return $"{Base}?{string.Join("&", parts)}";
        }
    }

    // ── 보상 지급 이력 Admin (AdminRewardGrantsController: Route = "api/admin/reward-grants") ──
    public static class AdminRewardGrants
    {
        private const string Base = "api/admin/reward-grants";

        /// <summary>단건 상세 조회 (GET)</summary>
        public static string ById(int id) => $"{Base}/{id}";

        /// <summary>필터 + 페이지네이션 검색</summary>
        public static string Search(int? playerId, int? sourceType, string? sourceKey,
            DateTime? from, DateTime? to, int page, int pageSize)
        {
            var parts = new List<string>();
            if (playerId.HasValue) parts.Add($"playerId={playerId.Value}");
            if (sourceType.HasValue) parts.Add($"sourceType={sourceType.Value}");
            if (!string.IsNullOrEmpty(sourceKey)) parts.Add($"sourceKey={Uri.EscapeDataString(sourceKey)}");
            if (from.HasValue) parts.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
            if (to.HasValue) parts.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
            parts.Add($"page={page}");
            parts.Add($"pageSize={pageSize}");
            return $"{Base}?{string.Join("&", parts)}";
        }
    }

    // ── 수동 보상 지급 Admin (AdminRewardDispatchController: Route = "api/admin/reward-dispatch") ──
    public static class AdminRewardDispatch
    {
        /// <summary>단일 수동 보상 지급 (POST)</summary>
        public const string Grant = "api/admin/reward-dispatch/grant";
    }

    // ── 매치 이력 Admin (AdminMatchesController: Route = "api/admin/matches") ──
    public static class AdminMatches
    {
        private const string Base = "api/admin/matches";

        /// <summary>매치 단건 상세 조회 (GET)</summary>
        public static string ById(Guid id) => $"{Base}/{id}";

        /// <summary>필터 + 페이지네이션 검색</summary>
        public static string Search(Guid? matchId, int? playerId, int? tier, int? state,
            DateTime? from, DateTime? to, int page, int pageSize)
        {
            var parts = new List<string>();
            if (matchId.HasValue) parts.Add($"matchId={matchId.Value}");
            if (playerId.HasValue) parts.Add($"playerId={playerId.Value}");
            if (tier.HasValue) parts.Add($"tier={tier.Value}");
            if (state.HasValue) parts.Add($"state={state.Value}");
            if (from.HasValue) parts.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
            if (to.HasValue) parts.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
            parts.Add($"page={page}");
            parts.Add($"pageSize={pageSize}");
            return $"{Base}?{string.Join("&", parts)}";
        }
    }

    // ── 광고 정책 Admin (AdminAdPoliciesController: Route = "api/admin/ad-policies") ──
    public static class AdminAdPolicies
    {
        private const string Base = "api/admin/ad-policies";

        /// <summary>목록 조회 (GET) / 생성 (POST)</summary>
        public const string Collection = Base;

        /// <summary>단건 조회 (GET) / 수정 (PUT) / 소프트 삭제 (DELETE)</summary>
        public static string ById(int id) => $"{Base}/{id}";

        /// <summary>필터 + 페이지네이션 검색</summary>
        public static string Search(int? network, int page, int pageSize)
        {
            var parts = new List<string>();
            if (network.HasValue) parts.Add($"network={network.Value}");
            parts.Add($"page={page}");
            parts.Add($"pageSize={pageSize}");
            return $"{Base}?{string.Join("&", parts)}";
        }
    }

    // ── 인앱결제 상품 Admin (AdminIapProductsController: Route = "api/admin/iap/products") ──
    public static class AdminIapProducts
    {
        private const string Base = "api/admin/iap/products";

        /// <summary>목록 조회 (GET) / 생성 (POST)</summary>
        public const string Collection = Base;

        /// <summary>단건 수정 (PUT) / 소프트 삭제 (DELETE)</summary>
        public static string ById(int id) => $"{Base}/{id}";

        /// <summary>필터 + 페이지네이션 검색</summary>
        public static string Search(int? store, int? productType, bool? isEnabled, int page, int pageSize)
        {
            var parts = new List<string>();
            if (store.HasValue)       parts.Add($"store={store.Value}");
            if (productType.HasValue) parts.Add($"productType={productType.Value}");
            if (isEnabled.HasValue)   parts.Add($"isEnabled={isEnabled.Value.ToString().ToLower()}");
            parts.Add($"page={page}");
            parts.Add($"pageSize={pageSize}");
            return $"{Base}?{string.Join("&", parts)}";
        }
    }

    // ── 인앱결제 구매 이력 Admin (AdminIapPurchasesController: Route = "api/admin/iap/purchases") ──
    public static class AdminIapPurchases
    {
        private const string Base = "api/admin/iap/purchases";

        /// <summary>필터 + 페이지네이션 검색</summary>
        public static string Search(int? playerId, int? store, string? productId, int? status,
            DateTime? from, DateTime? to, int page, int pageSize)
        {
            var parts = new List<string>();
            if (playerId.HasValue)             parts.Add($"playerId={playerId.Value}");
            if (store.HasValue)                parts.Add($"store={store.Value}");
            if (!string.IsNullOrEmpty(productId)) parts.Add($"productId={Uri.EscapeDataString(productId)}");
            if (status.HasValue)               parts.Add($"status={status.Value}");
            if (from.HasValue)                 parts.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
            if (to.HasValue)                   parts.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
            parts.Add($"page={page}");
            parts.Add($"pageSize={pageSize}");
            return $"{Base}?{string.Join("&", parts)}";
        }
    }

    // ── 레벨 임계값 Admin (AdminLevelThresholdsController: Route = "api/admin/level-thresholds") ──
    public static class AdminLevelThresholds
    {
        /// <summary>전체 조회 (GET) / 일괄 교체 (PUT)</summary>
        public const string Collection = "api/admin/level-thresholds";
    }

    // ── 스테이지 Admin (AdminStagesController: Route = "api/admin/stages") ──
    public static class AdminStages
    {
        private const string Base = "api/admin/stages";

        /// <summary>목록 조회 (GET) / 생성 (POST)</summary>
        public const string Collection = Base;

        /// <summary>단건 조회 (GET) / 수정 (PUT)</summary>
        public static string ById(int id) => $"{Base}/{id}";

        /// <summary>키워드 + 페이지네이션 검색</summary>
        public static string Search(string? keyword, int page, int pageSize)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(keyword)) parts.Add($"keyword={Uri.EscapeDataString(keyword)}");
            parts.Add($"page={page}");
            parts.Add($"pageSize={pageSize}");
            return $"{Base}?{string.Join("&", parts)}";
        }
    }

    // ── SignalR 허브 경로 ──────────────────────────────────────────────────
    public static class Hubs
    {
        /// <summary>매치메이킹 SignalR 허브 경로 (ApiBaseUrl 뒤에 붙이는 상대 경로)</summary>
        public const string Matchmaking = "/hubs/matchmaking";
    }
}
