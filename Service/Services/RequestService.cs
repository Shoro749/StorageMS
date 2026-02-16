using Data.Context;
using Data.Models;
using Repository.Interfaces;
using Repository.Repositories;
using Service.Interfaces;

namespace Service.Services
{
    public class RequestService : Service<OutgoingRequest>, IRequestService
    {
        private readonly IRequestRepository _repository;

        public RequestService(DataContext context) : base(context)
        {
            _repository = new RequestRepository(context);
        }

        public async Task<List<OutgoingRequest>> GetRequestWithItems()
        {
            return await _repository.GetRequestWithItems();
        }
    }
}
