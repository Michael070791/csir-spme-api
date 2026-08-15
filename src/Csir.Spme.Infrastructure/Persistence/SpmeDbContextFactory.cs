using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Csir.Spme.Infrastructure.Persistence;

public class SpmeDbContextFactory : IDesignTimeDbContextFactory<SpmeDbContext>
{
    public SpmeDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0
            ? args[0]
            : "Server=localhost;Database=CsirSpmeV2;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var builder = new DbContextOptionsBuilder<SpmeDbContext>();
        builder.UseSqlServer(connectionString);

        return new SpmeDbContext(builder.Options);
    }
}
