using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkAnalytics.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkClicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LinkClicks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShortLinkId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClickedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkClicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkClicks_ShortLinks_ShortLinkId",
                        column: x => x.ShortLinkId,
                        principalTable: "ShortLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LinkClicks_ShortLinkId_ClickedAt",
                table: "LinkClicks",
                columns: new[] { "ShortLinkId", "ClickedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinkClicks");
        }
    }
}
