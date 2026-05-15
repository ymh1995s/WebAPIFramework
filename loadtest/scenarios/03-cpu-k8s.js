// 시나리오 03 — CPU 바운드 (K8s 환경)
// 목적: K8s 파드 분산 환경에서 PBKDF2 CPU 연산의 처리량·응답시간 측정
// HPA(Horizontal Pod Autoscaler) 동작 여부 확인: kubectl get hpa -w
// 파드 분산 확인: kubectl get pods -o wide -w
// 사전 준비: kind K8s 클러스터 구동 + Framework.Api 파드 배포 (NodePort 30080)
import http from 'k6/http';
import { check, sleep } from 'k6';
import { stressStages } from '../config/stages.js';
import { stressThresholds } from '../config/thresholds.js';

// K8s NodePort 기본값 30080 — BASE_URL 환경변수로 오버라이드 가능
const BASE_URL = __ENV.BASE_URL || 'http://localhost:30080';

// PBKDF2 반복 횟수 — 환경변수로 조정 가능 (기본 50,000회)
const ITERATIONS = __ENV.ITERATIONS || 50000;

export const options = {
    stages: stressStages,
    thresholds: stressThresholds,
};

export default function () {
    // CPU 바운드 엔드포인트 호출 — PBKDF2(SHA-256) 연산 수행
    // 응답의 machineName/podHost로 어느 파드가 처리했는지 확인 가능
    const res = http.get(`${BASE_URL}/api/load-test/cpu?iterations=${ITERATIONS}`);

    check(res, {
        // 응답 상태 200 확인
        '상태 200': (r) => r.status === 200,
        // 응답 본문에 podHost 필드 포함 확인 (K8s 파드 식별)
        'podHost 존재': (r) => JSON.parse(r.body).podHost !== undefined,
    });

    // VU당 1초 간격 — active VU가 stages target과 일치하도록 sleep 강제
    sleep(1);
}
