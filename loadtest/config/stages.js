// 스트레스 테스트 단계 설정 — 점진적 VU 증가 후 피크 유지
// 10 → 50 → 100 → 200 → 400 VU 순서로 부하 증가, 마지막에 0으로 감소
export const stressStages = [
    { duration: '30s',  target: 10  },  // 워밍업: 1분간 0 → 10 VU
    { duration: '1m',  target: 50  },  // 점증: 2분간 10 → 50 VU
    { duration: '1m',  target: 100 },  // 점증: 2분간 50 → 100 VU
    { duration: '1m',  target: 200 },  // 점증: 2분간 100 → 200 VU
    { duration: '1m30s',  target: 400 },  // 피크: 3분간 200 → 400 VU 유지
    { duration: '30s',  target: 0   },  // 쿨다운: 1분간 400 → 0 VU
];
