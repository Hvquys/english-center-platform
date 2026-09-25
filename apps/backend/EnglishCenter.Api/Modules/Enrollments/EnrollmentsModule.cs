namespace EnglishCenter.Api.Modules.Enrollments;

public static class EnrollmentsModule
{
    public static IServiceCollection AddEnrollmentsModule(this IServiceCollection services)
    {
        services.AddScoped<IEnrollmentService, EnrollmentService>();
        return services;
    }
}
