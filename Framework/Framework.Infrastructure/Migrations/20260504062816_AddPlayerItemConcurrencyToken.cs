using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerItemConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin은 PostgreSQL 모든 테이블에 자동 존재하는 시스템 컬럼.
            // EF Core가 그림자 속성으로 인식하면서 AddColumn을 자동 생성하지만,
            // 실제로 컬럼을 추가하려 하면 PostgreSQL이 시스템 컬럼 충돌로 거부함.
            // 따라서 본 마이그레이션은 모델 스냅샷만 갱신하고 DDL은 수행하지 않음.
            // (낙관적 동시성 토큰 적용은 DbContext 매핑만으로 이미 완성됨)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Up이 빈 마이그레이션이므로 Down도 빈 처리.
        }
    }
}
