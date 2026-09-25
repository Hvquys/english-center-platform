namespace EnglishCenter.Api.Modules.Teachers;

public static class TeachersModule
{
    public static IServiceCollection AddTeachersModule(this IServiceCollection services)
    {
        services.AddScoped<ITeacherService, TeacherService>();
        return services;
    }
}
