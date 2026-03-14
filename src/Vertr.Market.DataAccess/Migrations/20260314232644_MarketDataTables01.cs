using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vertr.Market.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MarketDataTables01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "access_control");

            migrationBuilder.CreateTable(
                name: "market_trades",
                schema: "access_control",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    json_content = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("market_trades_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "open_interests",
                schema: "access_control",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    json_content = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("open_interests_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_books",
                schema: "access_control",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    json_content = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("order_books_pkey", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "market_trades",
                schema: "access_control");

            migrationBuilder.DropTable(
                name: "open_interests",
                schema: "access_control");

            migrationBuilder.DropTable(
                name: "order_books",
                schema: "access_control");
        }
    }
}
