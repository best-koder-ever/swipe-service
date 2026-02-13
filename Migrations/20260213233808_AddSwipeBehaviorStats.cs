using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwipeService.Migrations
{
    /// <inheritdoc />
    public partial class AddSwipeBehaviorStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SwipeBehaviorStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TotalSwipes = table.Column<int>(type: "int", nullable: false),
                    TotalLikes = table.Column<int>(type: "int", nullable: false),
                    TotalPasses = table.Column<int>(type: "int", nullable: false),
                    RightSwipeRatio = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    AvgSwipeVelocity = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    PeakSwipeStreak = table.Column<int>(type: "int", nullable: false),
                    CurrentConsecutiveLikes = table.Column<int>(type: "int", nullable: false),
                    RapidSwipeCount = table.Column<int>(type: "int", nullable: false),
                    DaysActive = table.Column<int>(type: "int", nullable: false),
                    SwipeTrustScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 100m),
                    LastCalculatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FlaggedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FlagReason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastSwipeAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CooldownUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwipeBehaviorStats", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SwipeBehaviorStats_TrustScore",
                table: "SwipeBehaviorStats",
                column: "SwipeTrustScore");

            migrationBuilder.CreateIndex(
                name: "IX_SwipeBehaviorStats_UserId",
                table: "SwipeBehaviorStats",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SwipeBehaviorStats");
        }
    }
}
