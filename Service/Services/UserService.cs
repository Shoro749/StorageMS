using Data.Context;
using Data.Models;
using Repository.Interfaces;
using Repository.Repositories;
using Service.Interfaces;

namespace Service.Services
{
    public class UserService : Service<User>, IUserService
    {
        private readonly IUserRepository _repository;
        public UserService(DataContext context) : base(context)
        {
            _repository = new UserRepository(context);
        }

        public async Task<User> GetByUsername(string username)
        {
            return await _repository.GetByUsername(username);
        }
    }
}
