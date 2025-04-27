using System.ComponentModel.DataAnnotations;

namespace VeCatch.Models
{
    public class Trainer
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public int SpriteIndex { get; set; }
        public List<Pokemon>? CaughtPokemon { get; set; }
        public int UltraBalls { get; set; } = 0;
        public string? Team1 { get; set; }
        public string? Team2 { get; set; }
        public string? Team3 { get; set; }
        public string? Team4 { get; set; }
        public string? Team5 { get; set; }
        public string? Team6  { get; set; }
    }
}
