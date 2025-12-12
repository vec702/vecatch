using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace VeCatch.Models
{
    public class PokemonDatabase : DbContext
    {
        public DbSet<Pokemon> Pokemon { get; set; }

        public PokemonDatabase(DbContextOptions<PokemonDatabase> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pokemon>().ToTable("Pokemon");
        }

    }
}
