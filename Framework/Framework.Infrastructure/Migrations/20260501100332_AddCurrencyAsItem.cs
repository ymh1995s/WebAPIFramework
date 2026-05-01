using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyAsItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Gold/Gems 시드 데이터 삽입 및 IsCurrency 플래그 설정
            // Id=1(Gold), Id=2(Gems)는 예약된 통화 아이템 — 이미 존재하면 IsCurrency만 갱신
            migrationBuilder.Sql(@"
                INSERT INTO ""Items"" (""Id"", ""Name"", ""Description"", ""IsCurrency"", ""ItemType"", ""AuditLevel"", ""AnomalyThreshold"", ""IsDeleted"")
                VALUES (1, 'Gold', '', true, 0, 0, 0, false), (2, 'Gems', '', true, 0, 0, 0, false)
                ON CONFLICT (""Id"") DO UPDATE SET ""IsCurrency"" = true;
            ");

            // 기존 PlayerProfile.Gold → PlayerItem(ItemId=1) 이전
            // 이미 PlayerItem 행이 있으면 수량을 누적 (ON CONFLICT)
            migrationBuilder.Sql(@"
                INSERT INTO ""PlayerItems"" (""PlayerId"", ""ItemId"", ""Quantity"")
                SELECT ""Id"", 1, ""Gold"" FROM ""PlayerProfiles"" WHERE ""Gold"" > 0
                ON CONFLICT (""PlayerId"", ""ItemId"") DO UPDATE SET ""Quantity"" = ""PlayerItems"".""Quantity"" + EXCLUDED.""Quantity"";
            ");

            // 기존 PlayerProfile.Gems → PlayerItem(ItemId=2) 이전
            migrationBuilder.Sql(@"
                INSERT INTO ""PlayerItems"" (""PlayerId"", ""ItemId"", ""Quantity"")
                SELECT ""Id"", 2, ""Gems"" FROM ""PlayerProfiles"" WHERE ""Gems"" > 0
                ON CONFLICT (""PlayerId"", ""ItemId"") DO UPDATE SET ""Quantity"" = ""PlayerItems"".""Quantity"" + EXCLUDED.""Quantity"";
            ");

            // 미수령 우편의 Mail.Gold → MailItem(ItemId=1) 이전
            // 이미 수령된 우편(IsClaimed=true)은 건너뜀 — 이미 지급 완료 상태
            migrationBuilder.Sql(@"
                INSERT INTO ""MailItems"" (""MailId"", ""ItemId"", ""Quantity"")
                SELECT ""Id"", 1, ""Gold"" FROM ""Mails"" WHERE ""Gold"" > 0 AND ""IsClaimed"" = false;
            ");

            // 미수령 우편의 Mail.Gems → MailItem(ItemId=2) 이전
            migrationBuilder.Sql(@"
                INSERT INTO ""MailItems"" (""MailId"", ""ItemId"", ""Quantity"")
                SELECT ""Id"", 2, ""Gems"" FROM ""Mails"" WHERE ""Gems"" > 0 AND ""IsClaimed"" = false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 데이터 이전 롤백 — MailItems에서 통화 항목 제거
            migrationBuilder.Sql(@"
                DELETE FROM ""MailItems"" WHERE ""ItemId"" IN (1, 2);
            ");

            // 롤백: PlayerItem 통화 행 제거
            migrationBuilder.Sql(@"
                DELETE FROM ""PlayerItems"" WHERE ""ItemId"" IN (1, 2);
            ");

            // 시드 데이터 롤백 — Gold/Gems 아이템 IsCurrency 플래그 초기화
            migrationBuilder.Sql(@"
                UPDATE ""Items"" SET ""IsCurrency"" = false WHERE ""Id"" IN (1, 2);
            ");
        }
    }
}
