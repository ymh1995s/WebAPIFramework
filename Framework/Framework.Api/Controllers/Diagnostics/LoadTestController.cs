#if LOADTEST

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Cryptography;

namespace Framework.Api.Controllers.Diagnostics;

// 부하테스트 진단 컨트롤러
// 컴파일 조건: LOADTEST 심볼이 정의된 빌드에서만 컴파일
//   - 부하테스트 빌드 (Release + LOADTEST 정의) → 컴파일 포함
//   - 운영 빌드 (LOADTEST 미정의) → 컴파일 제외 → 운영 보안 유지
// 인증 우회(AllowAnonymous) + Rate Limit 제외(DisableRateLimiting) 적용 — 부하테스트 시 외부 변수 제거
[AllowAnonymous]
[DisableRateLimiting]
[ApiController]
[Route("api/load-test")]
public class LoadTestController : ControllerBase
{
    // 핑 응답 DTO — 서버 식별 정보 반환
    private record PingResponse(
        string MachineName,   // 서버 호스트명 (컨테이너 환경에서 파드 식별)
        string PodHost        // K8s 파드 호스트명 (HOSTNAME 환경변수, 없으면 "n/a")
    );

    // CPU 부하 응답 DTO — 연산 결과 및 소요 시간 반환
    private record CpuResponse(
        string MachineName,   // 서버 호스트명
        int ProcessId,        // 현재 프로세스 ID (멀티 인스턴스 환경에서 프로세스 구분)
        int Iterations,       // 실제 실행된 PBKDF2 반복 횟수 (clamp 적용 후)
        long ElapsedMs,       // 연산 소요 시간 (밀리초)
        string PodHost        // K8s 파드 호스트명
    );

    // 핑 엔드포인트 — 서버 생존 확인 및 파드 식별용 (CPU 연산 없음)
    // K8s 멀티 파드 환경에서 라운드로빈 분산 확인에 활용
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new PingResponse(
            MachineName: Environment.MachineName,
            PodHost: Environment.GetEnvironmentVariable("HOSTNAME") ?? "n/a"
        ));
    }

    // CPU 바운드 엔드포인트 — PBKDF2(SHA-256) 연산으로 실제 CPU 부하 생성
    // iterations: PBKDF2 반복 횟수 (1_000 ~ 500_000 clamp, 기본값 50_000)
    // 부하테스트 시나리오 01(단일)/03(K8s) 에서 사용
    [HttpGet("cpu")]
    public IActionResult Cpu([FromQuery] int iterations = 50_000)
    {
        // 안전 범위 clamp — 너무 낮으면 의미 없는 수치, 너무 높으면 서버 과부하
        var clamped = Math.Clamp(iterations, 1_000, 500_000);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // PBKDF2(SHA-256) 해시 연산 — CPU 바운드 작업 시뮬레이션
        // 임의의 고정 salt/password 사용 (연산 목적이므로 보안 의미 없음)
        var password = new byte[16];
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        // .NET 10+ 권장 방식: 정적 Pbkdf2 메서드 사용 (생성자 방식 deprecated)
        _ = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: clamped,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);

        stopwatch.Stop();

        return Ok(new CpuResponse(
            MachineName: Environment.MachineName,
            ProcessId: Environment.ProcessId,
            Iterations: clamped,
            ElapsedMs: stopwatch.ElapsedMilliseconds,
            PodHost: Environment.GetEnvironmentVariable("HOSTNAME") ?? "n/a"
        ));
    }
}

#endif
