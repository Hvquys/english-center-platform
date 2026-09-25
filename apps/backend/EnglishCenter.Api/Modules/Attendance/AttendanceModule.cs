namespace EnglishCenter.Api.Modules.Attendance;

public static class AttendanceModule
{
    public static IServiceCollection AddAttendanceModule(this IServiceCollection services)
    {
        services.AddScoped<IAttendanceService, AttendanceService>();
        return services;
    }
}
