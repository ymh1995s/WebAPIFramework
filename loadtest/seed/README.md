# 부하테스트 시드 데이터

랭킹 DB 시나리오(02, 04)를 위한 PostgreSQL 시드 데이터.

## 시드 규모

| 테이블 | 행 수 | 식별 방법 |
|---|---|---|
| Players | 1,000명 | `Nickname LIKE 'loadtest_player_%'` |
| GameResults | 10,000매치 | `State = 2` (Finished) |
| GameResultParticipants | ~1,000,000행 | 위 Players + GameResults 참조 |

## 적용 방법

### 1. PostgreSQL 컨테이너 확인

```bash
docker ps
# 컨테이너 이름 확인 (예: webapi-postgres, postgres 등)
```

### 2. 시드 적용

```bash
docker exec -i <postgres-container> psql -U <user> -d <db> < seed-ranking.sql
```

예시 (일반적인 로컬 설정):

```bash
docker exec -i postgres psql -U postgres -d framework < loadtest/seed/seed-ranking.sql
```

컨테이너명/계정/DB명은 `deploy/docker-compose/.env` 또는 `appsettings.json` 의 ConnectionStrings:Default 에서 확인.

### 3. 적용 확인

```sql
SELECT COUNT(*) FROM "Players" WHERE "Nickname" LIKE 'loadtest_player_%';
-- 결과: 1000

SELECT COUNT(*) FROM "GameResultParticipants";
-- 결과: ~1,000,000
```

## 적용 시간

약 5~15초 (서버 사양, PostgreSQL I/O 성능에 따라 다름).

## 시드 정리

```bash
docker exec -i <postgres-container> psql -U <user> -d <db> < loadtest/seed/cleanup-ranking.sql
```

정리 순서: GameResultParticipants → GameResults → Players (FK 순서 보장).

## 주의사항

- 이 시드는 **개발/테스트 환경 전용**입니다. 운영 DB에 절대 적용 금지.
- `loadtest_player_` 접두사로 시드 데이터를 식별하므로, 운영 플레이어 닉네임에 이 접두사를 사용하지 마세요.
- 시드 적용 전 기존 loadtest 데이터가 있다면 cleanup을 먼저 실행하세요.
- `GameResultParticipants`의 실제 삽입 수는 `UNIQUE(MatchId, PlayerId)` 제약으로 인해 1,000,000보다 약간 적을 수 있습니다.
