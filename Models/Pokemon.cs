using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace VeCatch.Models
{
    #region Pokemon Type Enum
    public enum PokemonType
    {
        Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground, Flying,
        Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy, None
    }
    #endregion
    #region Pokedex Class
    public static class Pokedex
    {
        public static async Task<Pokemon?> GetPokemonFromDbAsync(int id, bool isShiny = false)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PokemonDatabase>();
            optionsBuilder.UseSqlite("Data Source=pokemon.db");
            using var db = new PokemonDatabase(optionsBuilder.Options);

            var existingPokemon = await db.Pokemon.FirstOrDefaultAsync(p => p.Id == id);

            if (existingPokemon == null)
            {
                Console.WriteLine($"No Pokémon found with ID {id}.");
                return null;
            }
            return existingPokemon;
        }

        public static async Task<Pokemon?> GetPokemonFromDbAsyncByName(string name, bool isShiny = false)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PokemonDatabase>();
            optionsBuilder.UseSqlite("Data Source=pokemon.db");
            using var db = new PokemonDatabase(optionsBuilder.Options);
            var existingPokemon = await db.Pokemon.FirstOrDefaultAsync(p => p.Name == name);
            return existingPokemon;
        }
        public static async Task<Pokemon?> GetPokemonAsync(string name, int level, bool isShiny=false)
        {
            using HttpClient client = new();
            string url = $"https://pokeapi.co/api/v2/pokemon/{name.ToLower()}";

            try
            {
                var response = await client.GetStringAsync(url);
                var json = JsonNode.Parse(response);

                if (json is null) return null;

                name = json["name"]?.GetValue<string>() ?? "";

                var cryData = json["cries"]?["latest"]?.GetValue<string>() ?? "";

                int baseHP = json?["stats"]?[0]?["base_stat"]?.GetValue<int>() ?? 10;
                int baseAttack = json?["stats"]?[1]?["base_stat"]?.GetValue<int>() ?? 10;
                int baseDefense = json?["stats"]?[2]?["base_stat"]?.GetValue<int>() ?? 10;
                int baseSpAtk = json?["stats"]?[3]?["base_stat"]?.GetValue<int>() ?? 10;
                int baseSpDef = json?["stats"]?[4]?["base_stat"]?.GetValue<int>() ?? 10;
                int baseSpeed = json?["stats"]?[5]?["base_stat"]?.GetValue<int>() ?? 10;
                int pokedexNumber = json?["id"]?.GetValue<int>() ?? 1;

                var type1Value = json?["types"]?[0]?["type"]?["name"]?.GetValue<string>();
                PokemonType type1;
                if (!Enum.TryParse(type1Value, true, out type1)) { type1 = PokemonType.None; }
                var type2Value = json?["types"]?.AsArray().Count > 1
                    ? json?["types"]?[1]?["type"]?["name"]?.GetValue<string>()
                    : null;
                PokemonType type2;
                if (!Enum.TryParse(type2Value, true, out type2)) { type2 = PokemonType.None; }

                string? speciesUrl = json?["species"]?["url"]?.GetValue<string>();
                var speciesResponse = await client.GetStringAsync(speciesUrl);
                var speciesJson = JsonNode.Parse(speciesResponse);
                int catchRate = speciesJson?["capture_rate"]?.GetValue<int>() ?? 45;

                var sprites = json?["sprites"];
                // animated sprites from pokemon showdown
                //string? defaultSprite = sprites?["other"]?["showdown"]?["front_default"]?.GetValue<string>() ?? "";
                //string? shinySprite = sprites?["other"]?["showdown"]?["front_shiny"]?.GetValue<string>() ?? "";
                string? defaultSprite = "";
                string? shinySprite = "";

                // fallback and use non-animated sprites if there is none available
                if (defaultSprite == "")
                {
                    defaultSprite = sprites?["front_default"]?.GetValue<string>() ?? "";
                }

                if(shinySprite == "")
                {
                    shinySprite = sprites?["front_shiny"]?.GetValue<string>() ?? "";
                }

                string spriteUrl = string.Empty;

                if (isShiny)
                {
                    spriteUrl = shinySprite;
                    name = $"✨Shiny {name}✨";
                } else
                {
                    spriteUrl = defaultSprite;
                }

                return new Pokemon(name, level, baseHP, baseAttack, baseDefense, baseSpAtk, baseSpDef, baseSpeed, type1, type2, catchRate)
                {
                    SpriteUrl = spriteUrl,
                    Cry = cryData,
                    NationalDexNo = pokedexNumber
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Pokémon data: {ex.Message}");
                return null;
            }
        }
    }
    #endregion
    #region Pokemon Class
    public class Pokemon
    {
        [Key]
        public int Id {  get; set; }
        public string Name { get; set; } = "";
        public int Level { get; set; }
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int SpecialAttack { get; set; }
        public int SpecialDefense { get; set; }
        public int Speed { get; set; }
        public int CatchRate { get; set; }
        public string? SpriteUrl { get; set; }
        public PokemonType Type1 { get; set; }
        public PokemonType Type2 { get; set; }
        public int TrainerId { get; set; }
        public string? Cry {  get; set; }
        public int NationalDexNo { get; set; }
        public string? DefaultSprite {  get; set; }
        public string? ShinySprite {  get; set; }
        public Pokemon()
        {

        }

        public Pokemon(string name, int level, int baseHP, int baseAttack, int baseDefense, int baseSpAtk, int baseSpDef, int baseSpeed, PokemonType type1, PokemonType type2, int catchRate, bool userSpawn = false)
        {
            Name = name;
            Level = level;
            Type1 = type1;
            Type2 = type2;

            if(userSpawn)
            {
                MaxHP = baseHP;
                CurrentHP = MaxHP;
                Attack = baseAttack;
                Defense = baseDefense;
                SpecialAttack = baseSpAtk;
                SpecialDefense = baseSpDef;
                Speed = baseSpeed;
            }
            else
            {
                MaxHP = CalculateStat(baseHP, level, isHP: true);
                CurrentHP = MaxHP;
                Attack = CalculateStat(baseAttack, level);
                Defense = CalculateStat(baseDefense, level);
                SpecialAttack = CalculateStat(baseSpAtk, level);
                SpecialDefense = CalculateStat(baseSpDef, level);
                Speed = CalculateStat(baseSpeed, level);
            }
            CatchRate = catchRate;
        }

        private int CalculateStat(int baseStat, int level, bool isHP = false)
        {
            if (isHP)
                return ((2 * baseStat * level) / 100) + level + 10;
            else
                return ((2 * baseStat * level) / 100) + 5;
        }

        public void TakeDamage(int damage)
        {
            CurrentHP -= damage;
            if (CurrentHP < 0) CurrentHP = 0;
        }

        public void Heal(int amount)
        {
            CurrentHP += amount;
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
        }

        public bool IsFainted() => CurrentHP <= 0;

        public override string ToString()
        {
            string typeInfo = Type2 == PokemonType.None ? Type1.ToString() : $"{Type1} / {Type2}";
            return $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Name)} | Type: {typeInfo} | Catch Rate: {CatchRate} | Health: {CurrentHP}/{MaxHP} | Attack: {Attack} | Defense: {Defense} | Sp. Attack: {SpecialAttack} | Sp. Defense: {SpecialDefense} | Speed: {Speed}";
        }
    }
    #endregion
    #region API Response Class
    public class PokeApiResponse
    {
        [JsonPropertyName("stats")]
        public PokeStat[]? Stats { get; set; }

        [JsonPropertyName("types")]
        public PokeTypeSlot[]? Types { get; set; }
    }
    public class PokeSpeciesResponse
    {
        [JsonPropertyName("capture_rate")]
        public int CaptureRate { get; set; }
    }
    public class PokeStat
    {
        [JsonPropertyName("base_stat")]
        public int BaseStat { get; set; }
    }

    public class PokeTypeSlot
    {
        [JsonPropertyName("type")]
        public PokeType? Type { get; set; }
    }

    public class PokeType
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
    public static class PokemonTypeExtensions
    {
        public static PokemonType ToPokemonType(this string typeName)
        {
            return typeName.ToLower() switch
            {
                "normal" => PokemonType.Normal,
                "fire" => PokemonType.Fire,
                "water" => PokemonType.Water,
                "grass" => PokemonType.Grass,
                "electric" => PokemonType.Electric,
                "ice" => PokemonType.Ice,
                "fighting" => PokemonType.Fighting,
                "poison" => PokemonType.Poison,
                "ground" => PokemonType.Ground,
                "flying" => PokemonType.Flying,
                "psychic" => PokemonType.Psychic,
                "bug" => PokemonType.Bug,
                "rock" => PokemonType.Rock,
                "ghost" => PokemonType.Ghost,
                "dragon" => PokemonType.Dragon,
                "dark" => PokemonType.Dark,
                "steel" => PokemonType.Steel,
                "fairy" => PokemonType.Fairy,
                _ => PokemonType.None
            };
        }
    }
    #endregion
}
