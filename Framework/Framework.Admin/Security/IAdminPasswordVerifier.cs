namespace Framework.Admin.Security;

// Admin 비밀번호 검증 인터페이스 — BCrypt 등 구현체 교체 가능하도록 추상화
public interface IAdminPasswordVerifier
{
    // 입력 비밀번호와 저장된 BCrypt 해시를 비교
    bool Verify(string? input);
}
