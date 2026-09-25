using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EnglishCenter.Api.Infrastructure.Persistence;

public sealed class EnglishCenterDbContextFactory
    : IDesignTimeDbContextFactory<EnglishCenterDbContext>
{
    public EnglishCenterDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer")
            ?? "Server=localhost;Database=EnglishCenter;Integrated Security=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<EnglishCenterDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new EnglishCenterDbContext(options);
    }
}
