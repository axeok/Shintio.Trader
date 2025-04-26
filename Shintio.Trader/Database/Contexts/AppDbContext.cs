using Microsoft.EntityFrameworkCore;

namespace Shintio.Trader.Database.Contexts;

public class AppDbContext : DbContext
{
    public AppDbContext() : base()
    {
    }

    public AppDbContext(DbContextOptions options) : base(options)
    {
    }
}