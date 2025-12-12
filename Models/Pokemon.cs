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
            if (existingPokemon is not null && isShiny)
            {
                if (!existingPokemon.Name.StartsWith("✨")) existingPokemon.Name = $"✨Shiny {existingPokemon.Name}✨";
            }
            return existingPokemon;
        }

        public static async Task<Pokemon?> GetPokemonFromDbByNameAsync(string name, bool isShiny = false)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PokemonDatabase>();
            optionsBuilder.UseSqlite("Data Source=pokemon.db");
            using var db = new PokemonDatabase(optionsBuilder.Options);
            var existingPokemon = await db.Pokemon.FirstOrDefaultAsync(p => p.Name == name);
            if (existingPokemon is not null && isShiny)
            {
                if(!existingPokemon.Name.StartsWith("✨")) existingPokemon.Name = $"✨Shiny {existingPokemon.Name}✨";
            }
            return existingPokemon;
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
