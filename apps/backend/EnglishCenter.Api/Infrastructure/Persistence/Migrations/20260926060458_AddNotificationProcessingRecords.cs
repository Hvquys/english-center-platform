using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishCenter.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationProcessingRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationProcessingRecords",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: false),
                    recipient_user_id = table.Column<long>(type: "bigint", nullable: false),
                    channel = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    template_key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    parameters_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    correlation_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationProcessingRecords", x => x.event_id);
                    table.CheckConstraint("CK_NotificationProcessingRecords_Channel", "[channel] IN ('EMAIL', 'IN_APP')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationProcessingRecords_ProcessedAtUtc",
                table: "NotificationProcessingRecords",
                column: "processed_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationProcessingRecords");
        }
    }
}
