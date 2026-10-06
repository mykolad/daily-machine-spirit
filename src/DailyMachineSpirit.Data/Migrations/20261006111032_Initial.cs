using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyMachineSpirit.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "machinespirit");

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "machinespirit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublishedOnUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    HereticalTruth = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GeneratedByModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BlessedCount = table.Column<int>(type: "int", nullable: false),
                    HeresyCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemProfiles",
                schema: "machinespirit",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ScoresGeneratorVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Scores = table.Column<byte[]>(type: "varbinary(8000)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemProfiles", x => x.ItemId);
                    table.ForeignKey(
                        name: "FK_ItemProfiles_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "machinespirit",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_PublishedOnUtc",
                schema: "machinespirit",
                table: "Items",
                column: "PublishedOnUtc",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemProfiles",
                schema: "machinespirit");

            migrationBuilder.DropTable(
                name: "Items",
                schema: "machinespirit");
        }
    }
}
