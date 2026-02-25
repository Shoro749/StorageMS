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

        public async Task<User?> UpdateUserAsync(int id, string name, string passwordHash, Role role)
        {
            var existing = await _dbSet
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (existing == null) return null;

            existing.Name = name;
            existing.PasswordHash = passwordHash;
            existing.Role = role;

            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
