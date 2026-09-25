namespace EnglishCenter.Api.Modules.Students;

public static class StudentsModule
{
    public static IServiceCollection AddStudentsModule(this IServiceCollection services)
    {
        services.AddScoped<IStudentService, StudentService>();
        return services;
    }
}
