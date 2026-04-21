namespace Framework.Application.Options;

// 매칭 설정값 - appsettings.json의 "MatchMaking" 섹션과 매핑
public class MatchMakingOptions
{
    // 매칭 성사에 필요한 최소 인원 수
    public int MaxPlayers { get; set; } = 4;
}
