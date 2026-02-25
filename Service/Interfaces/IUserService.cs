using Data.Models;

namespace Service.Interfaces
{
    public interface IUserService : IService<User>
    {
        Task<User> GetByUsername(string username);
        Task<User?> UpdateUserAsync(int id, string name, string passwordHash, Role role);
    }
}
