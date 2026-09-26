using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TmcTryoutSystem.Migrations
{
    /// <inheritdoc />
    public partial class CollegeStudentIdPicturesAndMultiSport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The UserId index is backed by a FK constraint, so it must be dropped first.
            migrationBuilder.DropForeignKey(
                name: "FK_Applicants_Users_UserId",
                table: "Applicants");

            migrationBuilder.DropIndex(
                name: "IX_Applicants_StudentId",
                table: "Applicants");

            migrationBuilder.DropIndex(
                name: "IX_Applicants_UserId",
                table: "Applicants");

            migrationBuilder.AlterColumn<string>(
                name: "YearLevel",
                table: "Applicants",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldMaxLength: 30)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CourseOrGrade",
                table: "Applicants",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldMaxLength: 80)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IdPictureBackPath",
                table: "Applicants",
                type: "varchar(260)",
                maxLength: 260,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IdPictureFrontPath",
                table: "Applicants",
                type: "varchar(260)",
                maxLength: 260,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IdVerified",
                table: "Applicants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VerificationRemarks",
                table: "Applicants",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "Applicants",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerifiedById",
                table: "Applicants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_StudentId_SportId",
                table: "Applicants",
                columns: new[] { "StudentId", "SportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_UserId_SportId",
                table: "Applicants",
                columns: new[] { "UserId", "SportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_VerifiedById",
                table: "Applicants",
                column: "VerifiedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Applicants_Users_VerifiedById",
                table: "Applicants",
                column: "VerifiedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Applicants_Users_UserId",
                table: "Applicants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applicants_Users_UserId",
                table: "Applicants");

            migrationBuilder.DropForeignKey(
                name: "FK_Applicants_Users_VerifiedById",
                table: "Applicants");

            migrationBuilder.DropIndex(
                name: "IX_Applicants_StudentId_SportId",
                table: "Applicants");

            migrationBuilder.DropIndex(
                name: "IX_Applicants_UserId_SportId",
                table: "Applicants");

            migrationBuilder.DropIndex(
                name: "IX_Applicants_VerifiedById",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "IdPictureBackPath",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "IdPictureFrontPath",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "IdVerified",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "VerificationRemarks",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "VerifiedById",
                table: "Applicants");

            migrationBuilder.AlterColumn<string>(
                name: "YearLevel",
                table: "Applicants",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CourseOrGrade",
                table: "Applicants",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_StudentId",
                table: "Applicants",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_UserId",
                table: "Applicants",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Applicants_Users_UserId",
                table: "Applicants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
