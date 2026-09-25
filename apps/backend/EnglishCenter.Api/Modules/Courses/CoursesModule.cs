namespace EnglishCenter.Api.Modules.Courses;

public static class CoursesModule
{
    public static IServiceCollection AddCoursesModule(this IServiceCollection services)
    {
        services.AddScoped<ICourseService, CourseService>();
        return services;
    }
}
