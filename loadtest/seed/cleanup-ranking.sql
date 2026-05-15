-- =========================================================
-- 부하테스트 랭킹 시드 데이터 정리
-- 삭제 순서: GameResultParticipants → GameResults → Players
-- 식별 기준: Players.Nickname LIKE 'loadtest_player_%'
-- =========================================================

BEGIN;

-- ─────────────────────────────────────────────────────────
-- 1단계: 참가자 기록 삭제 (FK 의존성 먼저 제거)
-- loadtest 플레이어가 참가한 모든 GameResultParticipants 행 삭제
-- ─────────────────────────────────────────────────────────
DELETE FROM "GameResultParticipants"
WHERE "PlayerId" IN (
    SELECT "Id" FROM "Players"
    WHERE "Nickname" LIKE 'loadtest_player_%'
);

-- ─────────────────────────────────────────────────────────
-- 2단계: 게임 결과 삭제 (참가자 없는 매치 + 시드 매치 전체)
-- Finished 상태이며 참가자가 없는 매치 삭제
-- (이미 1단계에서 loadtest 플레이어 참가 기록이 삭제되었으므로,
--  참가자가 0명인 시드 매치를 추가로 정리)
-- ─────────────────────────────────────────────────────────
DELETE FROM "GameResults"
WHERE "State" = 2  -- Finished 상태
AND "Id" NOT IN (
    SELECT DISTINCT "MatchId" FROM "GameResultParticipants"
);

-- ─────────────────────────────────────────────────────────
-- 3단계: 플레이어 삭제 (loadtest_player_ 접두사로 식별)
-- IsDeleted 필터 우회 — 소프트 딜리트 플레이어도 포함하여 물리 삭제
-- ─────────────────────────────────────────────────────────
DELETE FROM "Players"
WHERE "Nickname" LIKE 'loadtest_player_%';

COMMIT;
