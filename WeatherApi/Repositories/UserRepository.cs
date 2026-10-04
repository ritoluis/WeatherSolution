using Microsoft.EntityFrameworkCore;
using WeatherApi.Data;
using WeatherApi.Entities;

namespace WeatherApi.Repositories;

public class UserRepository : IUserRepository
{
    private readonly WeatherDbContext _context;
    public UserRepository(WeatherDbContext context) => _context = context;

    public async Task<User?> GetByUsernameAsync(string username) =>
        await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

    public async Task<User?> GetByIdAsync(int id) =>
        await _context.Users.FindAsync(id);

    public async Task<IEnumerable<User>> GetAllAsync() =>
        await _context.Users.OrderBy(u => u.Username).ToListAsync();

    public async Task AddAsync(User user) =>
        await _context.Users.AddAsync(user);

    public async Task AddQueryAsync(UserQuery query) =>
        await _context.UserQueries.AddAsync(query);

    public async Task<IEnumerable<UserQuery>> GetAllQueriesAsync() =>
        await _context.UserQueries
            .Include(q => q.User)
            .OrderByDescending(q => q.QueryDate)
            .ToListAsync();

    public async Task<IEnumerable<UserQuery>> GetQueriesByUserIdAsync(int userId) =>
        await _context.UserQueries
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.QueryDate)
            .ToListAsync();

    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
}