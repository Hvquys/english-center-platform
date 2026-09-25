namespace EnglishCenter.Api.Modules.Classes;

public static class ClassesModule
{
    public static IServiceCollection AddClassesModule(this IServiceCollection services)
    {
        services.AddScoped<IClassService, ClassService>();
        return services;
    }
}
