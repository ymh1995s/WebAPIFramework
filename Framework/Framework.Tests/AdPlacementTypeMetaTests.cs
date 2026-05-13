using Framework.Application.Common;
using Framework.Domain.Enums;

namespace Framework.Tests;

// AdPlacementTypeMeta 레지스트리 완전성 검증 — enum 값이 추가될 때 누락 즉시 감지
public class AdPlacementTypeMetaTests
{
    [Fact]
    public void IsRegistered_모든_enum_값이_Registry에_등록되어야_한다()
    {
        foreach (var value in Enum.GetValues<AdPlacementType>())
            Assert.True(AdPlacementTypeMeta.IsRegistered(value), $"AdPlacementTypeMeta에 '{value}'이(가) 등록되지 않았습니다.");
    }
}
