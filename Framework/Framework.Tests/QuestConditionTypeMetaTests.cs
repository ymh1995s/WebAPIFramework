using Framework.Application.Common;
using Framework.Domain.Enums;

namespace Framework.Tests;

// QuestConditionTypeMeta 완전성 보장 테스트
public class QuestConditionTypeMetaTests
{
    [Fact]
    public void IsRegistered_모든_enum_값이_Registry에_등록되어야_한다()
    {
        var allValues = Enum.GetValues<QuestConditionType>();
        foreach (var value in allValues)
        {
            Assert.True(
                QuestConditionTypeMeta.IsRegistered(value),
                $"QuestConditionTypeMeta에 '{value}({(int)value})'이(가) 등록되지 않았습니다. Registry에 항목을 추가하세요.");
        }
    }
}
