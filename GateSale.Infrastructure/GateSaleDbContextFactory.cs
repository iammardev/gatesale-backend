using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using GateSale.Infrastructure.Data;

namespace GateSale.Infrastructure
{
    public class GateSaleDbContextFactory : IDesignTimeDbContextFactory<GateSaleDbContext>
    {
        public GateSaleDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<GateSaleDbContext>();

            // Replace with your actual connection string (use development env string)
            // var connectionString = "Host=gatesale-db.c3uu08mis7yb.af-south-1.rds.amazonaws.com;Port=5432;Database=gatesale;Username=gatesaledb;Password=gatesaledb;";
            var connectionString = "Host=localhost;Port=5432;Database=gatesale_local_2;Username=postgres;Password=postgres;";

            optionsBuilder.UseNpgsql(connectionString);

            return new GateSaleDbContext(optionsBuilder.Options);
        }
    }
}
