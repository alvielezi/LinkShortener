using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShortlyData.Models;

namespace ShortlyData
{
    public class AppDbContext:IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
         public DbSet<Url> Urls { get; set; }
         public DbSet<AppUser> Users { get; set; }
    }
}
