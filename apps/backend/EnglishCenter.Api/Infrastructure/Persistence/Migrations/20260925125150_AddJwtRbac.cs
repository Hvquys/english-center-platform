using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishCenter.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJwtRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    role = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    student_id = table.Column<long>(type: "bigint", nullable: true),
                    teacher_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.user_id);
                    table.CheckConstraint("CK_AppUsers_Role", "[role] IN ('ADMIN', 'STAFF', 'TEACHER', 'STUDENT')");
                    table.ForeignKey(
                        name: "FK_AppUsers_Students",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppUsers_Teachers",
                        column: x => x.teacher_id,
                        principalTable: "Teachers",
                        principalColumn: "teacher_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    refresh_token_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.refresh_token_id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AppUsers",
                        column: x => x.user_id,
                        principalTable: "AppUsers",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_AppUsers_Email",
                table: "AppUsers",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_AppUsers_StudentId",
                table: "AppUsers",
                column: "student_id",
                unique: true,
                filter: "[student_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_AppUsers_TeacherId",
                table: "AppUsers",
                column: "teacher_id",
                unique: true,
                filter: "[teacher_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserExpiry",
                table: "RefreshTokens",
                columns: new[] { "user_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "UQ_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "AppUsers");
        }
    }
}
