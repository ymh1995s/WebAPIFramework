// 시나리오 04 — DB 바운드 (K8s 환경)
// 목적: K8s 파드 분산 환경에서 랭킹 DB 조회의 처리량·응답시간 측정
// 단일 PostgreSQL 인스턴스(외부 Docker Compose)에 멀티 파드가 동시 쿼리하는 시나리오
// 사전 준비: kind K8s 클러스터 구동 + seed-ranking.sql 적용 (랭킹 데이터 필요)
// DEBUG 빌드의 인증 우회(PlayerId=1)로 토큰 없이 GET /api/ranking/me 호출 가능
import http from 'k6/http';
import { check, sleep } from 'k6';
import { stressStages } from '../config/stages.js';
import { stressThresholds } from '../config/thresholds.js';

// K8s NodePort 기본값 30080 — BASE_URL 환경변수로 오버라이드 가능
const BASE_URL = __ENV.BASE_URL || 'http://localhost:30080';

export const options = {
    stages: stressStages,
    thresholds: stressThresholds,
};

export default function () {
    // 랭킹 조회 엔드포인트 호출 — GameResultParticipants 집계 쿼리 발생
    // K8s 멀티 파드 환경에서 단일 DB 연결 풀 경합 측정
    const res = http.get(`${BASE_URL}/api/ranking/me`);

    check(res, {
        // 응답 상태 200 확인
        '상태 200': (r) => r.status === 200,
    });

    // VU당 1초 간격 — active VU가 stages target과 일치하도록 sleep 강제
    sleep(1);
}
