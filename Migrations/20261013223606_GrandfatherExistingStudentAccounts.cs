using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TmcTryoutSystem.Migrations
{
    /// <summary>
    /// Student accounts that already existed before email OTP and student-ID verification
    /// were introduced have neither a sent code nor uploaded ID photos. Mark them verified
    /// so the new sign-up gate does not lock existing students out of the site or of tryouts.
    /// Accounts created through the new flow always have an OTP and ID photos, so they are untouched.
    /// </summary>
    public partial class GrandfatherExistingStudentAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE `Users` SET `EmailVerified` = 1, `IdVerified` = 1 " +
                "WHERE `Role` = 'Student' AND `EmailVerified` = 0 " +
                "AND `OtpSentAt` IS NULL AND `IdPictureFrontPath` IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the previous state cannot be told apart afterwards.
        }
    }
}
