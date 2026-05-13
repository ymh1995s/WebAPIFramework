using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M_FixCurrencyItemType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Code가 없는 ItemType=Currency(0) 행을 Consumable(1)로 환원
            // — 진짜 통화 아이템은 M_AddItemCode에서 SOFT_CURRENCY/HARD_CURRENCY Code를 받았으므로
            //   Code IS NULL인 Currency 행은 잘못 분류된 일반 아이템 (예: 짱쌔맨검 @ Id=1)
            // — 새 환경에서는 해당 행이 없으므로 no-op
            migrationBuilder.Sql(@"
                UPDATE ""Items"" SET ""ItemType"" = 1 WHERE ""ItemType"" = 0 AND ""Code"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 롤백 불가 — 변경 전 ItemType을 알 수 없음
        }
    }
}
