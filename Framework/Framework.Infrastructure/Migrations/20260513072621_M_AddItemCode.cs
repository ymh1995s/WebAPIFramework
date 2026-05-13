using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M_AddItemCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Code 컬럼 추가
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // 기존 통화 행 Code 백필 — 현재 DB의 국문/영문 이름 모두 처리
            migrationBuilder.Sql(@"
                UPDATE ""Items"" SET ""Code"" = 'SOFT_CURRENCY'
                WHERE ""ItemType"" = 0 AND ""Code"" IS NULL
                AND ""Name"" IN ('Gold', '소프트재화', 'Soft Currency');

                UPDATE ""Items"" SET ""Code"" = 'HARD_CURRENCY'
                WHERE ""ItemType"" = 0 AND ""Code"" IS NULL
                AND ""Name"" IN ('Gems', '하드재화', 'Hard Currency', 'Gem');
            ");

            // unique 부분 인덱스 생성 — 백필 완료 후 생성해야 함
            migrationBuilder.CreateIndex(
                name: "IX_Items_Code",
                table: "Items",
                column: "Code",
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            // 통화 아이템이 없는 환경을 위한 시드 — WHERE NOT EXISTS로 멱등성 보장
            migrationBuilder.Sql(@"
                INSERT INTO ""Items"" (""Name"", ""Code"", ""ItemType"", ""Description"", ""AuditLevel"", ""AnomalyThreshold"", ""IsDeleted"")
                SELECT 'Gold', 'SOFT_CURRENCY', 0, '', 0, 0, false
                WHERE NOT EXISTS (SELECT 1 FROM ""Items"" WHERE ""Code"" = 'SOFT_CURRENCY');

                INSERT INTO ""Items"" (""Name"", ""Code"", ""ItemType"", ""Description"", ""AuditLevel"", ""AnomalyThreshold"", ""IsDeleted"")
                SELECT 'Gems', 'HARD_CURRENCY', 0, '', 0, 0, false
                WHERE NOT EXISTS (SELECT 1 FROM ""Items"" WHERE ""Code"" = 'HARD_CURRENCY');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_Code",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Items");
        }
    }
}
