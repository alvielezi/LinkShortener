

using ShortlyData.Models;

namespace Shortly.Data.Services
{
    public interface IUsersService
    { 
        Task<List<AppUser>> GetUsersAsync();
    }
}