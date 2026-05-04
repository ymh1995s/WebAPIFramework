using Framework.Application.Features.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Framework.Api.Services;

// JWT 토큰 생성 구현체
public class JwtTokenProvider : IJwtTokenProvider
{
    private readonly IConfiguration _config;

    public JwtTokenProvider(IConfiguration config)
    {
        _config = config;
    }

    // AccessToken 생성 - 내부 정수 Id는 서버 내부에서만 사용하고, 공개 식별자(publicId)만 클레임에 포함
    // playerId는 DB 조회용으로 저장만 하며 클라이언트에 노출하지 않음
    public string GenerateAccessToken(int playerId, Guid publicId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // playerId: 서버 내부 처리용 클레임 (미들웨어에서 int로 파싱하여 DB 조회에 사용)
        // publicId: 클라이언트에 반환하는 공개 식별자 클레임
        var claims = new[]
        {
            new Claim("playerId", playerId.ToString()),
            new Claim("publicId", publicId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // RefreshToken 생성 - 랜덤 바이트 기반 불투명 토큰 (30일 유효)
    public (string token, DateTime expiresAt) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes);
        return (token, DateTime.UtcNow.AddDays(30));
    }

    // 평문 리프래시 토큰을 SHA-256 해시(Base64 인코딩)로 변환 — DB 저장용
    // 입력: 평문 토큰 / 출력: 44자 고정 Base64 문자열
    // 인터페이스 구현 메서드 — 정적 메서드를 위임 호출하여 테스트·목킹에서도 사용 가능
    public string ComputeRefreshTokenHash(string plainToken) => HashRefreshToken(plainToken);

    // 정적 헬퍼 — Api 레이어 내부에서도 직접 호출 가능 (DI 없는 유틸 컨텍스트)
    public static string HashRefreshToken(string plainToken)
    {
        // SHA256.HashData: .NET 5+ 정적 메서드 — 내부적으로 SHA256 인스턴스 생성·해제를 처리
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToBase64String(hash);
    }
}
