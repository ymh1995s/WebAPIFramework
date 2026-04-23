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

    // ── 플레이어 기록 (PlayerRecordsController: Route = "api/playerrecords") ──
    public static class PlayerRecords
    {
        private const string Base = "api/playerrecords";

        /// <summary>전체 목록 페이지네이션 조회</summary>
        public static string Paged(int page, int pageSize) => $"{Base}?page={page}&pageSize={pageSize}";

        /// <summary>ID로 단건 조회</summary>
        public static string ById(int id) => $"{Base}/{id}";

        /// <summary>신규 기록 등록 (POST)</summary>
        public const string Create = Base;
    }

    // ── 랭킹 (RankingController: Route = "api/ranking") ───────────────────
    public static class Ranking
    {
        /// <summary>상위 N명 랭킹 조회</summary>
        public static string Top(int count) => $"api/ranking/top?count={count}";
    }

    // ── 인벤토리 (PlayerItemsController: Route = "api/players/{playerId}/items") ──
    public static class Players
    {
        /// <summary>특정 플레이어의 보유 아이템 목록 조회</summary>
        public static string Items(int playerId) => $"api/players/{playerId}/items";
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

        /// <summary>일일 보상 자동 발송 활성화 여부 조회(GET) / 변경(PUT)</summary>
        public const string DailyRewardEnabled = $"{Base}/daily-reward-enabled";
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

    // ── SignalR 허브 경로 ──────────────────────────────────────────────────
    public static class Hubs
    {
        /// <summary>매치메이킹 SignalR 허브 경로 (ApiBaseUrl 뒤에 붙이는 상대 경로)</summary>
        public const string Matchmaking = "/hubs/matchmaking";
    }
}
