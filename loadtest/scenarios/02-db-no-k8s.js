// 시나리오 02 — DB 바운드 (단일 서버, K8s 미적용)
// 목적: 단일 서버에서 랭킹 DB 조회(GameResultParticipants 집계)의 처리량·응답시간 측정
// 사전 준비: Framework.Api Debug 빌드 실행 + seed-ranking.sql 적용 (랭킹 데이터 필요)
// DEBUG 빌드의 인증 우회(PlayerId=1)로 토큰 없이 GET /api/ranking/me 호출 가능
import http from 'k6/http';
import { check, sleep } from 'k6';
import { stressStages } from '../config/stages.js';
import { stressThresholds } from '../config/thresholds.js';

// BASE_URL 환경변수 미지정 시 로컬 기본값 사용
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5058';

export const options = {
    stages: stressStages,
    thresholds: stressThresholds,
};

export default function () {
    // 랭킹 조회 엔드포인트 호출 — GameResultParticipants 집계 쿼리 발생
    // DEBUG 빌드 인증 우회로 Bearer 토큰 불필요 (PlayerId=1 고정)
    const res = http.get(`${BASE_URL}/api/ranking/me`);

    check(res, {
        // 응답 상태 200 확인
        '상태 200': (r) => r.status === 200,
    });

    // VU당 1초 간격 — active VU가 stages target과 일치하도록 sleep 강제
    sleep(1);
}
