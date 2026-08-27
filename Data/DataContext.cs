using apiprojnew.Models;
using Microsoft.EntityFrameworkCore;

namespace apiprojnew.Data
{
    public class DataContext : DbContext
    {

        public DbSet<User> Users { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<News> News { get; set; }




        public DataContext(DbContextOptions options) : base(options)
        {
        }

       
    }
}
