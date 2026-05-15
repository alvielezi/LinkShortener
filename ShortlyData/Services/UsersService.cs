using Microsoft.EntityFrameworkCore;
using ShortlyData;
using ShortlyData.Models;

namespace Shortly.Data.Services
{
    public class UsersService:IUsersService
    {
        private AppDbContext _context;
        public UsersService(AppDbContext context)
        { 
            _context = context;
        }
        public async Task<List<AppUser>> GetUsersAsync()
        {
            var users = await _context.Users.Include(n => n.Urls).ToListAsync();
            return users;
        }

    }
}