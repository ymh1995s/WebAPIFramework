using Framework.Application.Common;
using Framework.Domain.Enums;

namespace Framework.Tests;

// AdminNotificationCategoryMeta 완전성 보장 테스트
// 새 AdminNotificationCategory enum 값 추가 시 이 테스트가 실패하므로 Registry 미등록을 즉시 감지
public class AdminNotificationCategoryMetaTests
{
    [Fact]
    public void IsRegistered_모든_enum_값이_Registry에_등록되어야_한다()
    {
        var allValues = Enum.GetValues<AdminNotificationCategory>();
        foreach (var value in allValues)
        {
            Assert.True(
                AdminNotificationCategoryMeta.IsRegistered(value),
                $"AdminNotificationCategoryMeta에 '{value}({(int)value})'이(가) 등록되지 않았습니다. Registry에 항목을 추가하세요.");
        }
    }
}
