using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Players",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // 기존 행 백필 — 컬럼 추가 직후 기본값(0000...)인 행에 랜덤 UUID 부여
            // gen_random_uuid()는 PostgreSQL 내장 함수 (pgcrypto 없이 사용 가능, PG 13+)
            migrationBuilder.Sql(
                "UPDATE \"Players\" SET \"PublicId\" = gen_random_uuid() WHERE \"PublicId\" = '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_Players_PublicId",
                table: "Players",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Players_PublicId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Players");
        }
    }
}
