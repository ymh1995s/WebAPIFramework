// 스트레스 테스트 임계값 설정 — 테스트 통과/실패 기준
// p95: 1,000ms, p99: 3,000ms, 에러율: 5% 미만
// 게임 백엔드 비실시간 API의 일반적 SLA 수준 (인게임 실시간은 200ms 이하)
export const stressThresholds = {
    // 95번째 백분위 응답 시간 1,000ms 이내 — 95% 사용자가 1초 이내 응답 받음
    http_req_duration: ['p(95)<1000', 'p(99)<3000'],

    // 에러율 5% 미만 — HTTP 4xx/5xx 비율 허용 상한
    http_req_failed: ['rate<0.05'],
};
