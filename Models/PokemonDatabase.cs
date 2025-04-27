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

        public static async Task StorePokemonAsync(string name, bool isShiny = false)
        {
            using HttpClient client = new();
            string url = $"https://pokeapi.co/api/v2/pokemon/{name.ToLower()}";

            try
            {
                var response = await client.GetStringAsync(url);
                var json = JsonNode.Parse(response);
                if (json is null) return;

                name = json["name"]?.GetValue<string>() ?? "";
                var cryData = json["cries"]?["latest"]?.GetValue<string>() ?? "";
                int baseHP = json?["stats"]?[0]?["base_stat"]?.GetValue<int>() ?? 10;
                int baseAttack = json?["stats"]?[1]?["base_stat"]?.GetValue<int>() ?? 10;
                int baseDefense = json?["stats"]?[2]?["base_stat"]?.GetValue<int>() ?? 10;
                int baseSpAtk = json?["stats"]?[3]?["base_stat"]?.GetValue<int>() ?? 10;
                int baseSpDef = json?["stats"]?[4]?["base_stat"]?.GetValue<int>() ?? 10;
                int baseSpeed = json?["stats"]?[5]?["base_stat"]?.GetValue<int>() ?? 10;
                int pokedexNumber = json?["id"]?.GetValue<int>() ?? 1;

                string? speciesUrl = json?["species"]?["url"]?.GetValue<string>();
                var speciesResponse = await client.GetStringAsync(speciesUrl);
                var speciesJson = JsonNode.Parse(speciesResponse);
                int catchRate = speciesJson?["capture_rate"]?.GetValue<int>() ?? 45;

                var type1Value = json?["types"]?[0]?["type"]?["name"]?.GetValue<string>();
                PokemonType type1;
                if (!Enum.TryParse(type1Value, true, out type1)) { type1 = PokemonType.None; }
                var type2Value = json?["types"]?.AsArray().Count > 1
                    ? json?["types"]?[1]?["type"]?["name"]?.GetValue<string>()
                    : null;
                PokemonType type2;
                if (!Enum.TryParse(type2Value, true, out type2)) { type2 = PokemonType.None; }

                var sprites = json?["sprites"];
                int level = 50;
                
                string defaultSprite = "";
                string? shinySprite = "";

                // fallback and use non-animated sprites if there is none available
                if (defaultSprite == "")
                {
                    defaultSprite = sprites?["front_default"]?.GetValue<string>() ?? "";
                }

                if (shinySprite == "")
                {
                    shinySprite = sprites?["front_shiny"]?.GetValue<string>() ?? "";
                }

                string spriteUrl = string.Empty;

                if (isShiny)
                {
                    spriteUrl = shinySprite;
                    name = $"✨Shiny {name}✨";
                }
                else
                {
                    spriteUrl = defaultSprite;
                }

                using var db = new PokemonDatabase(new DbContextOptions<PokemonDatabase>());

                var existingPokemon = await db.Pokemon.FirstOrDefaultAsync(p => p.NationalDexNo == pokedexNumber);
                if (existingPokemon == null)
                {
                    var pokemon = new Pokemon(name, level, baseHP, baseAttack, baseDefense, baseSpAtk, baseSpDef, baseSpeed, type1, type2, catchRate)
                    {
                        SpriteUrl = spriteUrl,
                        Cry = cryData,
                        NationalDexNo = pokedexNumber,
                        DefaultSprite = defaultSprite,
                        ShinySprite = shinySprite
                    };

                    db.Pokemon.Add(pokemon);
                    await db.SaveChangesAsync();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Stored {name} in the database.");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{name} is already in the database.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor= ConsoleColor.Red;
                Console.WriteLine($"Error fetching Pokémon data: {ex.Message}");
                Console.ResetColor();
            }
        }

    }
}
