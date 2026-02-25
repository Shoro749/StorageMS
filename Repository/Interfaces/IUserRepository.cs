using Data.Models;

namespace Repository.Interfaces
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User> GetByUsername(string username);
        Task<User?> UpdateUserAsync(int id, string name, string passwordHash, Role role);
    }
}
