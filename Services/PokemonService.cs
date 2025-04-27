using System.Globalization;
using System.Reflection;
using System;
using VeCatch.Models;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Diagnostics.Eventing.Reader;

namespace VeCatch.Services
{
    public class PokemonService
    {
        private readonly Random _random = new();
        private readonly IDbContextFactory<DatabaseInfo> _dbContextFactory;
        private readonly ChatService _chatService;
        private readonly BattleService _battleService;
        private readonly AuthService _authService;

        public PokemonService(IDbContextFactory<DatabaseInfo> dbContextFactory, ChatService chatService, BattleService battleService, AuthService authService)
        {
            _dbContextFactory = dbContextFactory;
            _chatService = chatService;
            _battleService = battleService;
            _authService = authService;
        }

        public async Task<Pokemon?> GenerateRandomPokemon()
        {;
            int selectedPokedex = 0;
            int randomGeneration = new Random().Next(1, 10);
            switch (randomGeneration)
            {
                case 1:
                    selectedPokedex = new Random().Next(1, 152); // kanto
                    break;
                case 2:
                    selectedPokedex = new Random().Next(152, 252); // johto
                    break;
                case 3:
                    selectedPokedex = new Random().Next(252, 387); // hoenn
                    break;
                case 4:
                    selectedPokedex = new Random().Next(387, 511); // sinnoh / hisu
                    break;
                case 5:
                    selectedPokedex = new Random().Next(511, 667); // unova
                    break;
                case 6:
                    selectedPokedex = new Random().Next(667, 739); // kalos
                    break;
                case 7:
                    selectedPokedex = new Random().Next(739, 845); // alola
                    break;
                case 8:
                    selectedPokedex = new Random().Next(845,960); // galar
                    break;
                case 9:
                    selectedPokedex = new Random().Next(960, 1080); // paldea
                    break;
            }

            _battleService.Attackers = new List<string>();

            int ShinyChance = new Random().Next(1, 481);
            bool IsShiny = ShinyChance == 256 ? true : false;

            return await Pokedex.GetPokemonFromDbAsync(selectedPokedex, IsShiny);
        }

        public async Task<Pokemon?> GenerateRandomRaidPokemon()
        {
            int selectedPokedex = 0;
            int randomChance = new Random().Next(1, 3);

            switch(randomChance)
            {
                case 1:
                    // mega pokemon
                    selectedPokedex = new Random().Next(1080,1127);
                    break;
                case 2:
                    // dynamax pokemon
                    selectedPokedex = new Random().Next(1127,1161);
                    break;
            }

            _battleService.Attackers = new List<string>();

            int ShinyChance = new Random().Next(1, 513);
            bool IsShiny = ShinyChance == 256 ? true : false;

            return await Pokedex.GetPokemonFromDbAsync(selectedPokedex, IsShiny);
        }

        public async Task SavePokemon(Pokemon pkmn, Trainer t)
        {
            pkmn.TrainerId = t.Id;

            await using (var context = await _dbContextFactory.CreateDbContextAsync())
            {
                context.CaughtPokemon.Add(pkmn);
                await context.SaveChangesAsync();
            }
        }

    }
}
