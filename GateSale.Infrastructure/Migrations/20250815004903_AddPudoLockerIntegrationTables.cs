using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GateSale.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPudoLockerIntegrationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderTrackingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTrackingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderTrackingEvents_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PudoWebhookLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    RawPayload = table.Column<string>(type: "text", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingResult = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PudoWebhookLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserLockers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsSellerDropoff = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLockers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLockers_Lockers_LockerId",
                        column: x => x.LockerId,
                        principalTable: "Lockers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLockers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderTrackingEvents_OrderId",
                table: "OrderTrackingEvents",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLockers_LockerId",
                table: "UserLockers",
                column: "LockerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLockers_UserId_LockerId",
                table: "UserLockers",
                columns: new[] { "UserId", "LockerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderTrackingEvents");

            migrationBuilder.DropTable(
                name: "PudoWebhookLogs");

            migrationBuilder.DropTable(
                name: "UserLockers");
        }
    }
}
