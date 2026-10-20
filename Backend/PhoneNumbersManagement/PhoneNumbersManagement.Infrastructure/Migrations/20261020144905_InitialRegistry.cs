using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhoneNumbersManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ranges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RangeStart = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RangeEnd = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MainLine = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VoiceRoute = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NetboxSiteId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ranges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhoneNumbers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineUri = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NumericValue = table.Column<long>(type: "bigint", nullable: false),
                    RangeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedUser = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduledUnassignment = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhoneNumbers_Ranges_RangeId",
                        column: x => x.RangeId,
                        principalTable: "Ranges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservedFor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PhoneNumberId = table.Column<Guid>(type: "uuid", nullable: false),
                    System = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Expires = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TicketNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservations_PhoneNumbers_PhoneNumberId",
                        column: x => x.PhoneNumberId,
                        principalTable: "PhoneNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneNumbers_AssignedUser",
                table: "PhoneNumbers",
                column: "AssignedUser");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneNumbers_LineUri",
                table: "PhoneNumbers",
                column: "LineUri",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhoneNumbers_RangeId_Status_NumericValue",
                table: "PhoneNumbers",
                columns: new[] { "RangeId", "Status", "NumericValue" });

            migrationBuilder.CreateIndex(
                name: "IX_Ranges_Active_NetboxSiteId",
                table: "Ranges",
                columns: new[] { "Active", "NetboxSiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Expires",
                table: "Reservations",
                column: "Expires");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PhoneNumberId",
                table: "Reservations",
                column: "PhoneNumberId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "PhoneNumbers");

            migrationBuilder.DropTable(
                name: "Ranges");
        }
    }
}
