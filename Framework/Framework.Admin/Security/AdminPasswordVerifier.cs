using Microsoft.Extensions.Configuration;

namespace Framework.Admin.Security;

// BCrypt 기반 Admin 비밀번호 검증 구현체
public class AdminPasswordVerifier : IAdminPasswordVerifier
{
    // 설정에서 로드한 BCrypt 해시 — 시작 시 1회 읽음
    private readonly string? _hash;

    public AdminPasswordVerifier(IConfiguration config)
    {
        _hash = config["Admin:PasswordHash"];
    }

    // 입력값 또는 저장 해시가 비면 false 반환 — 빈 비밀번호 통과 방지
    public bool Verify(string? input)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(_hash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(input, _hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // 해시 형식 불량 — 설정 오류로 처리
            return false;
        }
    }
}
