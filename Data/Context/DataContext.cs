using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Data.Context
{
    public class DataContext : DbContext
    {
        //Add-Migration message -Project Data -StartupProject UI
        //Update-Database -Project Data -StartupProject UI
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var cnstring = "Server=localhost\\SQLEXPRESS01;Database=DbStorageMS;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
            optionsBuilder.UseSqlServer(cnstring);
            base.OnConfiguring(optionsBuilder);
        }

        public DbSet<User> Users => Set<User>();
    }
}
