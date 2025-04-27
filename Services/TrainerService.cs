using Microsoft.EntityFrameworkCore;
using System;
using VeCatch.Models;

namespace VeCatch.Services
{
    public class TrainerService
    {
        private readonly IDbContextFactory<DatabaseInfo> _dbContextFactory;

        public TrainerService(IDbContextFactory<DatabaseInfo> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<Pokemon?> GetPokemonFromDb(string name, Trainer trainer, bool IsShiny=false)
        {
            name = name.ToLower();

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var dbPokemon = await db.CaughtPokemon
                .Where(p => p.TrainerId == trainer.Id && p.Name == name)
                .FirstOrDefaultAsync();

            if (dbPokemon != null)
            {
                string spriteUrl = dbPokemon.SpriteUrl ?? "";
                string displayName = IsShiny ? $"✨Shiny {name}✨" : name;

                return new Pokemon(displayName, dbPokemon.Level, dbPokemon.MaxHP, dbPokemon.Attack,
                    dbPokemon.Defense, dbPokemon.SpecialAttack, dbPokemon.SpecialDefense, dbPokemon.Speed,
                    dbPokemon.Type1, dbPokemon.Type2, dbPokemon.CatchRate, true)
                {
                    SpriteUrl = spriteUrl,
                    Cry = dbPokemon.Cry
                };
            }
            else
            {
                return null;
            }
        }

        public async Task<Trainer?> GetTrainer(string name)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            return await db.Trainers.FirstOrDefaultAsync(t => t.Name == name);
        }

        public async Task<bool> HasPokemon(Trainer t, Pokemon p)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var caughtPokemon = await db.CaughtPokemon
                .Where(cp => cp.TrainerId == t.Id)
                .ToListAsync();

            var cleanedPokemonNames = caughtPokemon
                .Select(cp => CleanPokemonName(cp.Name))
                .ToList();

            return cleanedPokemonNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<bool> IsShiny(Trainer t, Pokemon p)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var caughtPokemon = await db.CaughtPokemon
                .FirstOrDefaultAsync(cp => cp.TrainerId == t.Id && cp.Name == $"✨Shiny {p.Name}✨");

            return caughtPokemon != null;
        }

        public async Task<bool> IsShinyByName(Trainer t, string p)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var targetName = $"✨Shiny {p}✨".ToLower();

            var caughtPokemon = await db.CaughtPokemon
                .FirstOrDefaultAsync(cp => cp.TrainerId == t.Id && cp.Name.ToLower() == targetName);

            return caughtPokemon != null;
        }

        private string CleanPokemonName(string name)
        {
            name = name.Replace("Shiny ", "", StringComparison.OrdinalIgnoreCase);
            if (name.Contains("✨"))
            {
                var cleanName = name.Split("✨")[1].Trim();
                return cleanName;
            }

            return name;
        }

        public async Task SaveTrainer(Chatter chatter)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == chatter.Name);
            if (trainer == null)
            {
                trainer = new Trainer { Name = chatter.Name, SpriteIndex = 1 };
                db.Trainers.Add(trainer);
                await db.SaveChangesAsync();
            }
        }
        public async Task UpdateTrainer(Trainer trainer)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            db.Trainers.Update(trainer);
            await db.SaveChangesAsync();
        }

    }
}
