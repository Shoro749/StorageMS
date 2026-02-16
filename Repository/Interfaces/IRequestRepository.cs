using Data.Models;

namespace Repository.Interfaces
{
    public interface IRequestRepository : IRepository<OutgoingRequest>
    {
        Task<List<OutgoingRequest>> GetRequestWithItems();
    }
}
