using apiprojnew.Models;
using Microsoft.EntityFrameworkCore;

namespace apiprojnew.Data
{
    public class DataContext : DbContext
    {

        public DbSet<User> Users { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<News> News { get; set; }
        public DbSet<NewsAttachment> NewsAttachments { get; set; }

        public DataContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<NewsAttachment>(entity =>
            {
                entity.HasOne(a => a.News)
                      .WithMany(n => n.Attachments)
                      .HasForeignKey(a => a.NewsId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
