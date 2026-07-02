# 게임 백엔드 프레임워크 (Game Backend Framework)

> 모바일 게임 서버를 처음부터 끝까지 혼자 설계·구현한 개인 프로젝트.
> 재화 정합성·부하·운영까지 실제 서비스를 고려한 ASP.NET Core 백엔드.

## ⚠️ 이 저장소 안내
- **이 저장소는 개발 초기 버전(중간 산출물)입니다.** 구조와 핵심 접근 방식을
  보여주기 위한 스냅샷이며, **완성된 최종본은 저작권 문제로 비공개**입니다.
- 최종본의 전체 동작은 아래 **YouTube 시연**에서 확인하실 수 있습니다.

## 프로젝트 개요
- **구분**: 개인 프로젝트
- **개발 기간**: 2026.04 ~ 2026.06 (3개월)
- **요약**: 클린 아키텍처 기반 5계층 구조 + 실물 도메인 배포까지 고려한 모바일 게임 백엔드

## 아키텍처 (Clean Architecture · 5 Layers)
> 실물 서버 컴퓨터 — 도메인: `api.overture.io.kr`
- **Api** — HTTP 요청 진입점
- **Admin** — Blazor 운영툴 UI (API 호출 + 렌더링)
- **Application** — 비즈니스 로직
- **Domain** — 엔티티·인터페이스 정의 (다른 계층 미참조)
- **Infrastructure** — DB 접근, EF Core 마이그레이션

## 기술 스택
| 구분 | 기술 |
|---|---|
| Web | ASP.NET Core (Web API + Blazor 운영툴) |
| ORM / DB | EF Core · PostgreSQL |
| 인증 | Guest / Google 로그인 + JWT |
| 캐싱 | Redis (Cache-Aside, DB 부하 분산) |
| 컨테이너 / 오케스트레이션 | Docker · Kubernetes (HPA) |
| 부하 테스트 | K6 (시나리오 4종) |
| 로그 · 모니터링 | Elasticsearch · Kibana · APM |

## 핵심 구현 포인트
- **재화 동시성 (Lost Update 차단)** — PostgreSQL `xmin`을 낙관적 동시성 토큰으로 사용, 충돌 시 재시도.
- **보상 멱등성** — `RewardGrant` UNIQUE 제약으로 중복 지급을 DB 레벨에서 원자적 차단.
- **캐시로 DB 부하 분산** — Redis Cache-Aside 도입, K6 부하 테스트로 도입 전후 비교 검증.
- **운영 고려** — K8s HPA 오토스케일링 · ELK·APM · Blazor 운영툴 · .bat 환경 구축 자동화 · 일일 DB 자동 백업.
- **실물 도메인 배포** — `api.overture.io.kr` 로 외부 접속까지 직접 구성.

## 데모 · 링크
- 🎥 **YouTube 시연**: https://www.youtube.com/watch?v=A7vO0yIkG5Q
- 🌐 배포 도메인: `api.overture.io.kr`
