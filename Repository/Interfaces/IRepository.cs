namespace Repository.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<List<T>> GetAllAsync();
        Task<T> AddAsync(T item);
        Task<T?> UpdateAsync(int id, T item);
        Task<bool> DeleteAsync(int id);
    }
}
