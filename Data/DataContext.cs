using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using CasperArmy_Chat.Entities;

namespace CasperArmy_Chat.Data
{
    public class DataContext: DbContext
    {
        protected readonly IConfiguration Configuration;

        public DataContext(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // in memory database used for simplicity, change to a real db for production applications
            options.UseNpgsql(Configuration.GetConnectionString("psqlCasperArmyServer"));
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Group>().ToTable("Groups");
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Join>().ToTable("Joins");
            modelBuilder.Entity<Message>().ToTable("Messages");
            modelBuilder.Entity<Upload>().ToTable("Uploads");
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups{ get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Join> Joins { get; set; }
        public DbSet<Upload> Uploads{ get; set; }
    }
}
