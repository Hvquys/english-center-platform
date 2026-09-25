namespace EnglishCenter.Api.Modules.Auth;

public static class AppRoles
{
    public const string Admin = "ADMIN";
    public const string Staff = "STAFF";
    public const string Teacher = "TEACHER";
    public const string Student = "STUDENT";
}

public static class AuthorizationPolicies
{
    public const string StaffOperations = "StaffOperations";
    public const string TeachingOperations = "TeachingOperations";
}
