using Data.Context;
using Repository.Interfaces;
using Repository.Repositories;
using Service.Interfaces;

namespace Service.Services
{
    public class Service<T> : IService<T> where T : class
    {
        private readonly IRepository<T> _repository;

        public Service(DataContext context) => _repository = new Repository<T>(context);

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<List<T>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<T> CreateAsync(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), $"Item {typeof(T)} is null!");

            return await _repository.AddAsync(item);
        }

        public async Task<T?> UpdateAsync(int id, T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), $"Item {typeof(T)} is null!");

            var updated = await _repository.UpdateAsync(id, item);

            if (updated == null)
                throw new KeyNotFoundException($"{typeof(T).Name} with id {id} not found");

            return updated;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteAsync(id);

            if (!deleted)
                throw new KeyNotFoundException($"{typeof(T).Name} with id {id} not found");

            return true;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _repository.ExistsAsync(id);
        }
    }
}