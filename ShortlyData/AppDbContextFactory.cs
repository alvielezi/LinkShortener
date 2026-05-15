using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShortlyData
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer("Data Source=ALVI-LAPTOP\\SQLEXPRESS01;Initial Catalog=ShortlyDB;Integrated Security=True;Encrypt=False");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}