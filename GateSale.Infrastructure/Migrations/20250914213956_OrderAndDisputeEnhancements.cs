using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GateSale.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrderAndDisputeEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdminFeeAmount",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AdminFeePercentage",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BuyerLockerId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CollectedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputeInitiatedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputeResolvedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnPaidBySeller",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ItemSubtotal",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PackageSize",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PudoShipmentReference",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PudoTrackingNumber",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnCompletedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnInitiatedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnShipmentReference",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnShippingCost",
                table: "Orders",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnTrackingNumber",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SellerLockerId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SellerPaidAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellerPayoutAmount",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingCost",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "Disputes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Disputes",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefundIssued",
                table: "Disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnCompleted",
                table: "Disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnPaidBySeller",
                table: "Disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnRequested",
                table: "Disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReasonCode",
                table: "Disputes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "Disputes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Disputes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Disputes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedById",
                table: "Disputes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DisputeEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisputeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    Caption = table.Column<string>(type: "text", nullable: true),
                    FileType = table.Column<string>(type: "text", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisputeEvidence_Disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "Disputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BuyerLockerId",
                table: "Orders",
                column: "BuyerLockerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SellerId",
                table: "Orders",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SellerLockerId",
                table: "Orders",
                column: "SellerLockerId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ReviewedById",
                table: "Disputes",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_DisputeId",
                table: "DisputeEvidence",
                column: "DisputeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Disputes_Users_ReviewedById",
                table: "Disputes",
                column: "ReviewedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Lockers_BuyerLockerId",
                table: "Orders",
                column: "BuyerLockerId",
                principalTable: "Lockers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Lockers_SellerLockerId",
                table: "Orders",
                column: "SellerLockerId",
                principalTable: "Lockers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Users_SellerId",
                table: "Orders",
                column: "SellerId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Disputes_Users_ReviewedById",
                table: "Disputes");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Lockers_BuyerLockerId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Lockers_SellerLockerId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Users_SellerId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "DisputeEvidence");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BuyerLockerId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SellerId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SellerLockerId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_ReviewedById",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "AdminFeeAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AdminFeePercentage",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BuyerLockerId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CollectedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DisputeInitiatedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DisputeResolvedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsReturnPaidBySeller",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemSubtotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PackageSize",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PudoShipmentReference",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PudoTrackingNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReturnCompletedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReturnInitiatedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReturnShipmentReference",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReturnShippingCost",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReturnTrackingNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SellerLockerId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SellerPaidAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SellerPayoutAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingCost",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "IsRefundIssued",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "IsReturnCompleted",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "IsReturnPaidBySeller",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "IsReturnRequested",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "Disputes");
        }
    }
}
