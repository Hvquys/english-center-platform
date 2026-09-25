using EnglishCenter.Api.Api;
using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Infrastructure.Persistence;
using EnglishCenter.Api.Modules.Attendance;
using EnglishCenter.Api.Modules.Classes;
using EnglishCenter.Api.Modules.Courses;
using EnglishCenter.Api.Modules.Enrollments;
using EnglishCenter.Api.Modules.Health;
using EnglishCenter.Api.Modules.Payments;
using EnglishCenter.Api.Modules.Students;
using EnglishCenter.Api.Modules.Teachers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string frontendCorsPolicy = "Frontend";

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddApiFoundation();

var sqlServerConnectionString = builder.Configuration.GetConnectionString("SqlServer")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:SqlServer is required. Configure it through environment variables or user secrets.");

builder.Services.AddDbContext<EnglishCenterDbContext>(options =>
    options.UseSqlServer(sqlServerConnectionString));

builder.Services.AddHealthModule();
builder.Services.AddAttendanceModule();
builder.Services.AddCoursesModule();
builder.Services.AddClassesModule();
builder.Services.AddEnrollmentsModule();
builder.Services.AddPaymentsModule();
builder.Services.AddStudentsModule();
builder.Services.AddTeachersModule();

builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(StatusCodeProblemDetails.WriteAsync);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(frontendCorsPolicy);
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
