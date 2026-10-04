using WeatherApi.Entities;

namespace WeatherApi.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(int id);
    Task<IEnumerable<User>> GetAllAsync();
    Task AddAsync(User user);
    Task AddQueryAsync(UserQuery query);
    Task<IEnumerable<UserQuery>> GetAllQueriesAsync();
    Task<IEnumerable<UserQuery>> GetQueriesByUserIdAsync(int userId);
    Task SaveChangesAsync();
}