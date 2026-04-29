using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <summary>
    /// GameMatches → GameResults, GameMatchParticipants → GameResultParticipants 테이블 이름 변경 마이그레이션
    /// 데이터 보존을 위해 Drop+Create 대신 RenameTable 사용 (자동 생성된 코드를 수동 수정)
    /// </summary>
    public partial class RenameGameMatchToGameResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FK 제약 먼저 제거 (테이블 이름이 바뀌면 FK 이름도 바뀌어야 하므로)
            migrationBuilder.DropForeignKey(
                name: "FK_GameMatchParticipants_GameMatches_MatchId",
                table: "GameMatchParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_GameMatchParticipants_Players_PlayerId",
                table: "GameMatchParticipants");

            // 인덱스 제거 (테이블 이름 변경 후 재생성)
            migrationBuilder.DropPrimaryKey(
                name: "PK_GameMatchParticipants",
                table: "GameMatchParticipants");

            migrationBuilder.DropIndex(
                name: "IX_GameMatchParticipants_MatchId_PlayerId",
                table: "GameMatchParticipants");

            migrationBuilder.DropIndex(
                name: "IX_GameMatchParticipants_PlayerId",
                table: "GameMatchParticipants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameMatches",
                table: "GameMatches");

            // 테이블 이름 변경 (데이터 보존)
            migrationBuilder.RenameTable(
                name: "GameMatches",
                newName: "GameResults");

            migrationBuilder.RenameTable(
                name: "GameMatchParticipants",
                newName: "GameResultParticipants");

            // PK 재생성
            migrationBuilder.AddPrimaryKey(
                name: "PK_GameResults",
                table: "GameResults",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameResultParticipants",
                table: "GameResultParticipants",
                column: "Id");

            // 인덱스 재생성
            migrationBuilder.CreateIndex(
                name: "IX_GameResultParticipants_MatchId_PlayerId",
                table: "GameResultParticipants",
                columns: new[] { "MatchId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameResultParticipants_PlayerId",
                table: "GameResultParticipants",
                column: "PlayerId");

            // FK 재생성
            migrationBuilder.AddForeignKey(
                name: "FK_GameResultParticipants_GameResults_MatchId",
                table: "GameResultParticipants",
                column: "MatchId",
                principalTable: "GameResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameResultParticipants_Players_PlayerId",
                table: "GameResultParticipants",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // FK 제약 먼저 제거
            migrationBuilder.DropForeignKey(
                name: "FK_GameResultParticipants_GameResults_MatchId",
                table: "GameResultParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_GameResultParticipants_Players_PlayerId",
                table: "GameResultParticipants");

            // 인덱스 제거
            migrationBuilder.DropPrimaryKey(
                name: "PK_GameResultParticipants",
                table: "GameResultParticipants");

            migrationBuilder.DropIndex(
                name: "IX_GameResultParticipants_MatchId_PlayerId",
                table: "GameResultParticipants");

            migrationBuilder.DropIndex(
                name: "IX_GameResultParticipants_PlayerId",
                table: "GameResultParticipants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameResults",
                table: "GameResults");

            // 테이블 이름 복원
            migrationBuilder.RenameTable(
                name: "GameResults",
                newName: "GameMatches");

            migrationBuilder.RenameTable(
                name: "GameResultParticipants",
                newName: "GameMatchParticipants");

            // PK 재생성
            migrationBuilder.AddPrimaryKey(
                name: "PK_GameMatches",
                table: "GameMatches",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameMatchParticipants",
                table: "GameMatchParticipants",
                column: "Id");

            // 인덱스 재생성
            migrationBuilder.CreateIndex(
                name: "IX_GameMatchParticipants_MatchId_PlayerId",
                table: "GameMatchParticipants",
                columns: new[] { "MatchId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameMatchParticipants_PlayerId",
                table: "GameMatchParticipants",
                column: "PlayerId");

            // FK 재생성
            migrationBuilder.AddForeignKey(
                name: "FK_GameMatchParticipants_GameMatches_MatchId",
                table: "GameMatchParticipants",
                column: "MatchId",
                principalTable: "GameMatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameMatchParticipants_Players_PlayerId",
                table: "GameMatchParticipants",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
