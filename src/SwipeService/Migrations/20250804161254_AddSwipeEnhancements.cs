using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwipeService.Migrations
{
    /// <inheritdoc />
    public partial class AddSwipeEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceInfo",
                table: "Swipes",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "MatchId",
                table: "Swipes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserLocation",
                table: "Swipes",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    User1Id = table.Column<int>(type: "int", nullable: false),
                    User2Id = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UnmatchedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UnmatchedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.CheckConstraint("CK_Match_UserOrder", "User1Id < User2Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Swipes_MatchId",
                table: "Swipes",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_UserId_TargetUserId",
                table: "Swipes",
                columns: new[] { "UserId", "TargetUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Match_User1Id",
                table: "Matches",
                column: "User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Match_User1Id_User2Id",
                table: "Matches",
                columns: new[] { "User1Id", "User2Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Match_User2Id",
                table: "Matches",
                column: "User2Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Swipes_Matches_MatchId",
                table: "Swipes",
                column: "MatchId",
                principalTable: "Matches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Swipes_Matches_MatchId",
                table: "Swipes");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Swipes_MatchId",
                table: "Swipes");

            migrationBuilder.DropIndex(
                name: "IX_UserId_TargetUserId",
                table: "Swipes");

            migrationBuilder.DropColumn(
                name: "DeviceInfo",
                table: "Swipes");

            migrationBuilder.DropColumn(
                name: "MatchId",
                table: "Swipes");

            migrationBuilder.DropColumn(
                name: "UserLocation",
                table: "Swipes");
        }
    }
}
