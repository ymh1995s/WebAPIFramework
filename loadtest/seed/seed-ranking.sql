-- =========================================================
-- 부하테스트 랭킹 시드 데이터
-- 대상 테이블: Players, GameResults, GameResultParticipants
-- 규모: Players 1,000명 / GameResults 10,000매치 / GameResultParticipants 1,000,000행
-- 실행 시간: ~5초 (PostgreSQL generate_series 활용)
-- 주의: loadtest_player_ 접두사로 식별 — cleanup-ranking.sql로 정리 가능
-- =========================================================

BEGIN;

-- ─────────────────────────────────────────────────────────
-- 0단계: 부하테스트용 PlayerId=1 명시 생성
-- 사유: API의 #if DEBUG || LOADTEST 인증 우회가 PlayerId=1 고정으로 ClaimsPrincipal을 주입.
--       DB에 Id=1 행이 없으면 /api/ranking/me 호출 시 NotFound 발생 → 부하테스트 불가.
-- OVERRIDING SYSTEM VALUE — PostgreSQL IDENTITY 컬럼에 명시 ID 강제 삽입.
-- ON CONFLICT — 이미 존재(예: 기존 개발 데이터)하면 skip하여 안전.
-- ─────────────────────────────────────────────────────────
INSERT INTO "Players" (
    "Id",
    "PublicId",
    "Nickname",
    "CreatedAt",
    "LastLoginAt",
    "IsBanned",
    "AttendanceCount",
    "IsDeleted"
)
OVERRIDING SYSTEM VALUE
VALUES (
    1,
    gen_random_uuid(),
    'loadtest_debug_player_1',
    NOW(),
    NOW(),
    false,
    0,
    false
)
ON CONFLICT ("Id") DO NOTHING;

-- ─────────────────────────────────────────────────────────
-- 1단계: 부하테스트 전용 플레이어 1,000명 삽입
-- Nickname = 'loadtest_player_N' 형식으로 식별 가능
-- DeviceId/GoogleId는 null (게스트 계정 불필요, 테스트 데이터이므로)
-- IsDeleted=false, IsBanned=false, AttendanceCount=0 기본값 사용
-- ─────────────────────────────────────────────────────────
INSERT INTO "Players" (
    "PublicId",
    "DeviceId",
    "GoogleId",
    "Nickname",
    "CreatedAt",
    "LastLoginAt",
    "IsBanned",
    "BannedUntil",
    "AttendanceCount",
    "IsDeleted",
    "DeletedAt",
    "MergedIntoPlayerId"
)
SELECT
    gen_random_uuid(),                          -- PublicId: 외부 공개용 UUID
    NULL,                                       -- DeviceId: 테스트용 null
    NULL,                                       -- GoogleId: 테스트용 null
    'loadtest_player_' || i,                    -- Nickname: 식별 가능한 고정 패턴
    NOW() - (random() * INTERVAL '90 days'),    -- CreatedAt: 최근 90일 내 랜덤
    NOW() - (random() * INTERVAL '7 days'),     -- LastLoginAt: 최근 7일 내 랜덤
    false,                                      -- IsBanned: 정상 계정
    NULL,                                       -- BannedUntil: 밴 없음
    0,                                          -- AttendanceCount: 기본값
    false,                                      -- IsDeleted: 정상 계정
    NULL,                                       -- DeletedAt: null
    NULL                                        -- MergedIntoPlayerId: null
FROM generate_series(1, 1000) AS i;

-- ─────────────────────────────────────────────────────────
-- 2단계: 게임 결과(매치) 10,000건 삽입
-- Tier는 1~10 중 랜덤 (Tier1~Tier10 → int 0~9, EF Core enum 매핑)
-- 모든 매치는 Finished 상태 (MatchState.Finished = 2)
-- ─────────────────────────────────────────────────────────
INSERT INTO "GameResults" (
    "Id",
    "Tier",
    "StartedAt",
    "EndedAt",
    "State"
)
SELECT
    gen_random_uuid(),                                      -- Id: Guid PK
    floor(random() * 10)::int,                             -- Tier: 0~9 (Tier1~Tier10)
    NOW() - (random() * INTERVAL '30 days'),               -- StartedAt: 최근 30일 내 랜덤
    NOW() - (random() * INTERVAL '30 days') + INTERVAL '10 minutes', -- EndedAt: StartedAt + ~10분
    2                                                       -- State: Finished (MatchState enum 값 2)
FROM generate_series(1, 10000);

-- ─────────────────────────────────────────────────────────
-- 3단계: 게임 결과 참가자 1,000,000행 삽입
-- 방식: 랭킹 조회 쿼리 부하 생성용 — PlayerId/MatchId 랜덤 조합
-- Score: 0~10,000 랜덤, HumanType: Human (0), Result: Win/Lose/Draw 랜덤(0~2)
-- 주의: UNIQUE(MatchId, PlayerId) 제약으로 실제 삽입 수는 약간 적을 수 있음
--       ON CONFLICT DO NOTHING으로 충돌 시 스킵 처리
-- 핵심: array를 CROSS JOIN으로 outer 쿼리에 끌어와야 random()이 행마다 평가됨
--       (scalar subquery는 PostgreSQL이 한 번만 평가해 캐싱 → 모든 행 동일 값)
-- ─────────────────────────────────────────────────────────
INSERT INTO "GameResultParticipants" (
    "MatchId",
    "PlayerId",
    "HumanType",
    "Score",
    "Result"
)
SELECT
    m_arr.arr[(floor(random() * array_length(m_arr.arr, 1)) + 1)::int]  AS match_id,
    p_arr.arr[(floor(random() * array_length(p_arr.arr, 1)) + 1)::int]  AS player_id,
    0,                                   -- HumanType: Human (0)
    floor(random() * 10001)::int,        -- Score: 0~10,000 랜덤
    floor(random() * 3)::int             -- Result: Win(0)/Lose(1)/Draw(2) 랜덤
FROM
    generate_series(1, 1200000) AS g,    -- 중복 충돌 감안하여 초과 생성 (목표 100만)
    (SELECT array_agg("Id") AS arr
     FROM "Players"
     WHERE "Nickname" LIKE 'loadtest_player_%') AS p_arr,
    (SELECT array_agg("Id") AS arr
     FROM (SELECT "Id" FROM "GameResults" WHERE "State" = 2 LIMIT 10000) m) AS m_arr
ON CONFLICT ("MatchId", "PlayerId") DO NOTHING;

COMMIT;
