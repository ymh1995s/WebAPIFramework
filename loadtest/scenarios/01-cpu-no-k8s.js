// 시나리오 01 — CPU 바운드 (단일 서버, K8s 미적용)
// 목적: 단일 프로세스 환경에서 PBKDF2 CPU 연산의 처리량·응답시간 측정
// 사전 준비: Framework.Api Debug 빌드 실행 (http://localhost:5058)
import http from 'k6/http';
import { check, sleep } from 'k6';
import { stressStages } from '../config/stages.js';
import { stressThresholds } from '../config/thresholds.js';

// BASE_URL 환경변수 미지정 시 로컬 기본값 사용
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5058';

// PBKDF2 반복 횟수 — 환경변수로 조정 가능 (기본 50,000회)
const ITERATIONS = __ENV.ITERATIONS || 50000;

export const options = {
    stages: stressStages,
    thresholds: stressThresholds,
};

export default function () {
    // CPU 바운드 엔드포인트 호출 — PBKDF2(SHA-256) 연산 수행
    const res = http.get(`${BASE_URL}/api/load-test/cpu?iterations=${ITERATIONS}`);

    check(res, {
        // 응답 상태 200 확인
        '상태 200': (r) => r.status === 200,
        // 응답 본문에 elapsedMs 필드 포함 확인
        'elapsedMs 존재': (r) => JSON.parse(r.body).elapsedMs !== undefined,
    });

    // VU당 1초 간격 — active VU가 stages target과 일치하도록 sleep 강제
    sleep(1);
}
