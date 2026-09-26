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
}
