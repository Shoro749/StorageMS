using Data.Context;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class RequestRepository : Repository<OutgoingRequest>, IRequestRepository
    {
        private readonly DataContext _context;

        public RequestRepository(DataContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<OutgoingRequest>> GetRequestWithItems()
        {
            return await _context.OutgoingRequests.Include(x => x.Items).Include(x => x.CreatedBy).ToListAsync();
        }
    }
}
