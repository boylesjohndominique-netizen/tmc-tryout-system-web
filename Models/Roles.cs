namespace TmcTryoutSystem.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Coach = "Coach";
    public const string Evaluator = "Evaluator";
    public const string Student = "Student";

    /// <summary>Everyone who works inside the system (not students).</summary>
    public const string Staff = "Admin,Coach,Evaluator";

    /// <summary>Roles allowed to record final selection decisions.</summary>
    public const string Deciders = "Admin,Coach";

    public static readonly string[] All = { Admin, Coach, Evaluator, Student };

    /// <summary>
    /// Roles the administrator manages on the Users page. Students are absent on purpose:
    /// they create their own accounts and are reviewed on the Applicant records page.
    /// </summary>
    public static readonly string[] StaffRoles = { Admin, Coach, Evaluator };
}

/// <summary>Claim types that carry a student's verification state into each request.</summary>
public static class VerificationClaims
{
    public const string EmailVerified = "EmailVerified";
    public const string IdVerified = "IdVerified";
}
