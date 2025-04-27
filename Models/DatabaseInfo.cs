using Microsoft.EntityFrameworkCore;

namespace VeCatch.Models
{
    public class DatabaseInfo : DbContext
    {
        public DbSet<Trainer> Trainers { get; set; }
        public DbSet<Pokemon> CaughtPokemon { get; set; }
        public DbSet<Info> Info { get; set; }

        public DatabaseInfo(DbContextOptions<DatabaseInfo> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Trainer>().ToTable("Trainers");
            modelBuilder.Entity<Pokemon>().ToTable("CaughtPokemon");
            modelBuilder.Entity<Info>().ToTable("Info");
        }
    }
}
