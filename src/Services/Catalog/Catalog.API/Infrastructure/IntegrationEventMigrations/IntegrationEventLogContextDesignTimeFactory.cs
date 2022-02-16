using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.eShopOnContainers.BuildingBlocks.IntegrationEventLogEF;
using System;

namespace Catalog.API.Infrastructure.IntegrationEventMigrations
{
    public class IntegrationEventLogContextDesignTimeFactory : IDesignTimeDbContextFactory<IntegrationEventLogContext>
    {
        public IntegrationEventLogContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<IntegrationEventLogContext>();

            optionsBuilder.UseMySql("server=192.168.31.6;user=root;password=a12345678;database=EShop;", new MySqlServerVersion(new Version(8, 0, 27)), options => options.MigrationsAssembly(GetType().Assembly.GetName().Name));

            return new IntegrationEventLogContext(optionsBuilder.Options);
        }
    }
}