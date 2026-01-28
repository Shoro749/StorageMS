using Data.Context;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(DataContext context) : base(context) { }

        public async Task<User> GetByUsername(string username)
        {
            return await _dbSet.Include(u => u.Role).FirstOrDefaultAsync(u => u.Name == username);
        }

        //public override async Task<List<User>> GetAllAsync()
        //{
        //    return await _dbSet.Include(u => u.Role).ToListAsync();
        //}
    }
}
