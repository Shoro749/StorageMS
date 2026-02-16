using Data.Models;

namespace Service.Interfaces
{
    public interface IRequestService : IService<OutgoingRequest>
    {
        Task<List<OutgoingRequest>> GetRequestWithItems();
    }
}
