namespace Data.Models
{
    public class BackupData
    {
        public DateTime BackupDate { get; set; }
        public List<User> Users { get; set; } = new();
        public List<Role> Roles { get; set; } = new();
        public List<Product> Products { get; set; } = new();
        public List<Incoming> Incomings { get; set; } = new();
        public List<OutgoingRequest> OutgoingRequests { get; set; } = new();
        public List<OutgoingItem> OutgoingItems { get; set; } = new();
        public List<ActionLog> ActionLogs { get; set; } = new();
    }
}
