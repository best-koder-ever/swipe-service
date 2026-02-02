using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwipeService.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserProfileMappings",
                columns: table => new
                {
                    ProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfileMappings", x => x.ProfileId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Swipes_User_Created",
                table: "Swipes",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Swipes_User_Like_Created",
                table: "Swipes",
                columns: new[] { "UserId", "IsLike", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileMapping_UserId",
                table: "UserProfileMappings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserProfileMappings");

            migrationBuilder.DropIndex(
                name: "IX_Swipes_User_Created",
                table: "Swipes");

            migrationBuilder.DropIndex(
                name: "IX_Swipes_User_Like_Created",
                table: "Swipes");
        }
    }
}
