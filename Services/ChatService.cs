using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Timers;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using VeCatch.Models;

namespace VeCatch.Services
{
    public class ChatService
    {
        private readonly string? _channelName;
        private readonly string? _botUsername;
        private TwitchClient? _client;
        private readonly System.Timers.Timer cleanupTimer;

        public event Func<Task>? OnChatUpdated;
        public event Func<string, string, Task>? OnTrainerSpriteChanged;
        public event Func<Chatter, Task>? OnCatchCommandReceived;
        public event Action<string, string>? OnChatMessageReceived;
        public event Func<Pokemon, bool, Task>? ThrowOutPokemon;
        public event Func<Pokemon, Task>? SetAttackingPokemon;
        public event Func<Task>? InvokeRemovePokemon;
        public event Func<Task>? InvokeRandomPokemon;
        public bool CONNECTED = false;
        public Pokemon? cs_currentPokemon = null;
        private readonly SemaphoreSlim _raidLock = new(1, 1);
        private readonly object _twitchLock = new();


        public List<Chatter> ActiveChatters { get; private set; } = new();
        public Dictionary<string, DateTime> ChatterActivity = new();
        private readonly IDbContextFactory<DatabaseInfo> _dbContextFactory;
        private readonly TrainerService _trainerService;
        private readonly BattleService _battleService;
        private readonly PokemonService _pokemonService;
        public static bool sentOutMonFainted = false;


        public ChatService(string channelName, IDbContextFactory<DatabaseInfo> dbContextFactory, TrainerService trainerService, BattleService battleService, PokemonService pokemonService)
        {
            _channelName = $"#{channelName}";
            _botUsername = channelName;
            _dbContextFactory = dbContextFactory;

            cleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1.0));
            cleanupTimer.Elapsed += RemoveInactiveChatters;
            cleanupTimer.AutoReset = true;
            cleanupTimer.Start();
            _trainerService = trainerService;
            _battleService = battleService;
            _pokemonService = pokemonService;
        }

        public void SetAccessToken(string accessToken)
        {
            if (_client != null && _client.IsConnected)
            {
                _client.Disconnect();
            }

            var credentials = new ConnectionCredentials(_botUsername, accessToken);
            _client = new TwitchClient();
            _client.Initialize(credentials, _channelName);

            _client.OnMessageReceived += Client_OnMessageReceived;
            _ = _client.Connect();
            CONNECTED = _client.IsConnected;
        }
        public void SendAlert(string message)
        {
            if (CONNECTED && _client != null)
            {
                lock (_twitchLock)
                {
                    try
                    {
                        _client.SendMessage(_channelName, $"/me {message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SendAlert error: {ex.Message}");
                    }
                }
            }
        }

        private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    string username = e.ChatMessage.DisplayName;
                    string message = e.ChatMessage.Message;
                    OnChatMessageReceived?.Invoke(username, message);
                    await HandleChatMessage(username, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ChatService] Error in message handler: {ex.Message}");
                }
            });
        }

        private async Task HandleChatMessage(string username, string message)
        {
            var existingChatter = ActiveChatters.FirstOrDefault(c => c.Name == username);
            if (existingChatter != null)
            {
                ChatterActivity[username] = DateTime.UtcNow;
            }
            else
            {
                var newChatter = new Chatter
                {
                    Name = username,
                    X = new Random().Next(0, 550),
                    Y = new Random().Next(0, 350)
                };
                ActiveChatters.Add(newChatter);
            }

            ChatterActivity[username] = DateTime.UtcNow;

            if (message.StartsWith("!"))
            {
                var chatter = ActiveChatters.FirstOrDefault(c => c.Name == username);

                if (chatter != null)
                {
                    await HandleCommand(chatter, message);
                }
            }

            await SafeInvokeChatUpdated();
        }

        private async Task RunRaidBattle(Trainer trainer)
        {
            await _raidLock.WaitAsync();
            try
            {
                var teamSlots = new[] { trainer.Team1, trainer.Team2, trainer.Team3, trainer.Team4, trainer.Team5, trainer.Team6 };

                try
                {
                    foreach (var monName in teamSlots)
                    {
                        sentOutMonFainted = false;
                        if (string.IsNullOrEmpty(monName)) break;
                        if (cs_currentPokemon == null || cs_currentPokemon.CurrentHP <= 0) break;

                        bool isShiny = await _trainerService.IsShinyByName(trainer, monName);
                        var attacker = await _trainerService.GetPokemonFromDb(monName, trainer, isShiny);
                        if (attacker == null) break;

                        attacker.CurrentHP = attacker.MaxHP;

                        if(SetAttackingPokemon is not null) await SetAttackingPokemon(attacker);
                        ChangeSprite(attacker);
                        await Task.Delay(2000);

                        while (cs_currentPokemon != null && cs_currentPokemon.CurrentHP > 0)
                        {
                            _battleService.AttackPokemon(attacker, cs_currentPokemon);
                            await Task.Delay(750);

                            if (cs_currentPokemon.CurrentHP > 0)
                            {
                                _battleService.AttackPokemon(cs_currentPokemon, attacker);
                                await Task.Delay(750);
                            }

                            if (attacker.CurrentHP <= 0)
                            {
                                sentOutMonFainted = true;
                                await Task.Delay(100);
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[RAID ERROR] ");
                    Console.ResetColor();
                    Console.WriteLine($"for {trainer.Name}: {ex}");
                }

                try
                {
                    if (cs_currentPokemon is not null && cs_currentPokemon.CurrentHP > 0)
                    {
                        SendAlert($"{trainer.Name}'s team was defeated!");
                    }
                    else if (cs_currentPokemon is not null && cs_currentPokemon.CurrentHP <= 0)
                    {
                        SendAlert($"{trainer.Name} defeated the raid boss!");
                    } else
                    {
                        sentOutMonFainted = true;
                        await Task.Delay(100);
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[RAID ERROR] ");
                    Console.ResetColor();
                    Console.WriteLine($"for {trainer.Name}: {ex}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[RAID ERROR] ");
                Console.ResetColor();
                Console.WriteLine($"for {trainer.Name}: {ex}");
            }
            finally
            {
                _raidLock.Release();
            }
        }


        private async void RemoveInactiveChatters(object? sender, ElapsedEventArgs e)
        {
            var now = DateTime.UtcNow;
            var inactiveUsers = ChatterActivity
                .Where(kvp => (now - kvp.Value).TotalMinutes >= 5)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var username in inactiveUsers)
            {
                ActiveChatters.RemoveAll(c => c.Name == username);
                ChatterActivity.Remove(username);
            }

            if (inactiveUsers.Any() && OnChatUpdated != null)
            {
                await SafeInvokeChatUpdated();
            }
        }
        private async Task SafeInvokeChatUpdated()
        {
            if (OnChatUpdated != null)
            {
                try
                {
                    await OnChatUpdated.Invoke();
                }
                catch (TaskCanceledException ex)
                {
                    Console.WriteLine($"[ChatService] UI Update Canceled: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ChatService] Unexpected error in UI update: {ex.Message}");
                }
            }
        }
        public event Func<MarkupString, Task>? ChatUpdateMessage;
        public async Task OnChatUpdateMessage(string s)
        {
            if (ChatUpdateMessage != null)
            {
                string message = $"\n{s}";
                await ChatUpdateMessage.Invoke(new MarkupString($"{message.Replace("\n", "<br />")}"));
                await Task.Delay(3000);
                await ChatUpdateMessage.Invoke(new MarkupString(""));
            }
        }
        private async Task<List<Pokemon>> LoadTeam(Trainer trainer)
        {
            var team = new List<Pokemon>();
            var slots = new[] { trainer.Team1, trainer.Team2, trainer.Team3, trainer.Team4, trainer.Team5, trainer.Team6 };

            foreach (var name in slots)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                bool shiny = await _trainerService.IsShinyByName(trainer, name);
                var mon = await _trainerService.GetPokemonFromDb(name, trainer, shiny);
                if (mon != null)
                {
                    mon.Heal(mon.MaxHP);
                    team.Add(mon);
                }
            }

            return team;
        }

        private async Task RunPvPBattle()
        {
            var attacker = _battleService.PvPChallenger;
            var defender = _battleService.PvPOpponent;

            if (attacker is null || defender is null) return;

            var attackerTeam = await LoadTeam(attacker);
            var defenderTeam = await LoadTeam(defender);

            int atkIndex = 0;
            int defIndex = 0;

            Pokemon? currentAttacker = null;
            Pokemon? currentDefender = null;

            while (atkIndex < attackerTeam.Count && defIndex < defenderTeam.Count)
            {
                sentOutMonFainted = false;

                var atkMon = attackerTeam[atkIndex];
                var defMon = defenderTeam[defIndex];

                if (ThrowOutPokemon is null) return;

                if(defMon != currentDefender)
                {
                    currentDefender = defMon;
                    await ThrowOutPokemon.Invoke(defMon, true);
                    await Task.Delay(500);
                    await OnChatUpdateMessage($"{defender.Name} sent out {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(defMon.Name)}!");
                    await Task.Delay(1500);
                }

                if(atkMon != currentAttacker)
                {
                    currentAttacker = atkMon;
                    ChangeSprite(atkMon);
                    await OnChatUpdateMessage($"{attacker.Name} sent out {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(atkMon.Name)}!");
                    await Task.Delay(2000);
                }

                while (atkMon.CurrentHP > 0 && defMon.CurrentHP > 0)
                {
                    if(atkMon.CurrentHP > 0)
                    {
                        _battleService.AttackPokemon(atkMon, defMon);
                        await Task.Delay(750);
                    }

                    if (defMon.CurrentHP > 0)
                    {
                        _battleService.AttackPokemon(defMon, atkMon);
                        await Task.Delay(750);
                    }

                    if (atkMon.CurrentHP <= 0)
                    {
                        await OnChatUpdateMessage($"{attacker.Name}'s {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(atkMon.Name)} fainted!");
                        sentOutMonFainted = true;
                        atkIndex++;
                        break;
                    }

                    if (defMon.CurrentHP <= 0)
                    {
                        await OnChatUpdateMessage($"{defender.Name}'s {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(defMon.Name)} fainted!");
                        if (InvokeRemovePokemon is not null) await InvokeRemovePokemon();
                        defIndex++;
                        break;
                    }
                }

                await Task.Delay(100);
            }

            string result;
            if (atkIndex >= attackerTeam.Count && defIndex >= defenderTeam.Count)
                result = "It's a tie!";
            else if (atkIndex >= attackerTeam.Count)
                result = $"{defender.Name} wins the PvP battle!";
            else
                result = $"{attacker.Name} wins the PvP battle!";

            SendAlert(result);
            await OnChatUpdateMessage(result);
            await Task.Delay(500);
            _battleService.IsPvP = false;
            _battleService.PvPChallenger = null;
            _battleService.PvPOpponent = null;
            cs_currentPokemon = null;
            if(InvokeRemovePokemon is not null) await InvokeRemovePokemon.Invoke();
        }



        public void ChangeSprite(Pokemon p)
        {
            if (p == null || p.SpriteUrl == null || p.Cry == null) return;
            if (SetAttackingPokemon is not null) _ = SetAttackingPokemon.Invoke(p);
            OnTrainerSpriteChanged?.Invoke(p.SpriteUrl, p.Cry);
        }
        private static string CleanArg(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return new string(input
                .Where(c => !char.IsControl(c) && !char.IsWhiteSpace(c) && !char.IsSurrogate(c) && !IsZeroWidthChar(c))
                .ToArray())
                .Trim();
        }
        // bttv / 7tv duplicate message fix
        private static bool IsZeroWidthChar(char c)
        {
            return c == '\u200B' || c == '\u200C' || c == '\u200D' || c == '\u2060' || c == '\uFEFF';
        }
        private async Task HandleCommand(Chatter chatter, string message)
        {
            string rawMessage = message.Trim();
            string[] split = rawMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = split[0].Trim();
            string[] args = split.Skip(1).ToArray();

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = CleanArg(args[i]);
            }

            Console.WriteLine($"{chatter.Name} used command: {command} {string.Join(" ", args)}");

            switch (command.ToLower())
            {
                // admin commands
                #region !givepokemon
                case "!givepokemon":
                    if (!string.Equals(chatter.Name, _channelName?.Replace("#", ""), StringComparison.OrdinalIgnoreCase)) break;
                    if (args?.Length >= 2)
                    {
                        if (string.IsNullOrEmpty(args[0]) || string.IsNullOrEmpty(args[1]))
                        {
                            SendAlert($"{chatter.Name}, usage: !givepokemon [pokémon name] [chatter name] [optional: shiny]");
                            break;
                        }

                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        Trainer? trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == args[1]);
                        if (trainer == null)
                        {
                            SendAlert($"{args[1]} doesn't have a trainer profile!");
                            break;
                        }
                        Pokemon? pokemon = null;
                        if (!string.IsNullOrEmpty(args[2]) && args[2].ToLower() == "shiny") {
                            pokemon = await _pokemonService.GetPokemonByNameAsync(args[0], true);
                        }
                        else pokemon = await _pokemonService.GetPokemonByNameAsync(args[0], false);
                        if (pokemon == null)
                        {
                            SendAlert($"\"{args[0]}\" is not a valid Pokémon!");
                            break;
                        }
                        SendAlert($"{trainer.Name} has received a {pokemon.Name}!");
                        await _pokemonService.SavePokemon(pokemon, trainer);
                    }
                    break;
                #endregion
                #region !superlure
                case "!pokelure":
                    if (!string.Equals(chatter.Name, _channelName?.Replace("#", ""), StringComparison.OrdinalIgnoreCase)) break;
                    else if (InvokeRandomPokemon is not null) await InvokeRandomPokemon.Invoke();
                    break;
                #endregion

                // user commands
                #region !trainer
                case "!trainer":
                    if (args?.Length == 1 && !string.IsNullOrEmpty(args[0]))
                    {
                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == args[0]);
                        if (trainer == null)
                        {
                            SendAlert($"{args[0]} doesn't have a trainer profile!");
                            break;
                        }
                        SendAlert($"https://vec702.duckdns.org/channel/{_botUsername}/trainer/{trainer.Id}");
                    }
                    else
                    {
                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == chatter.Name);
                        if (trainer == null)
                        {
                            SendAlert($"{chatter.Name}, you don't have a trainer profile!");
                            break;
                        }
                        SendAlert($"https://vec702.duckdns.org/channel/{_botUsername}/trainer/{trainer.Id}");
                    }
                    break;
                #endregion
                #region !inventory
                case "!inventory":
                    if (args?.Length == 1 && !string.IsNullOrEmpty(args[0]))
                    {
                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == args[0]);
                        if (trainer == null)
                        {
                            SendAlert($"{args[0]} doesn't have a trainer profile!");
                            break;
                        }
                        SendAlert($"{trainer.Name} has {trainer.UltraBalls} Ultra Ball(s).");
                    }
                    else
                    {
                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == chatter.Name);
                        if (trainer == null)
                        {
                            SendAlert($"{chatter.Name}, you don't have a trainer profile!");
                            break;
                        }
                        SendAlert($"{trainer.Name} has {trainer.UltraBalls} Ultra Ball(s).");
                    }
                    break;
                #endregion
                #region !catchattack
                case "!catchattack":
                    {
                        if (_battleService.Attackers.Contains(chatter.Name))
                            return;

                        if (_battleService.IsPvP) return;

                        if (cs_currentPokemon == null)
                            break;

                        if (_battleService.IsRaidActive) return;

                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await db.Trainers
                            .Include(t => t.CaughtPokemon)
                            .FirstOrDefaultAsync(t => t.Name == chatter.Name);

                        if (trainer == null || string.IsNullOrEmpty(trainer.Name))
                        {
                            SendAlert($"{chatter.Name}, you don't have a trainer profile!");
                            _battleService.Attackers.Remove(chatter.Name);
                            break;
                        }

                        await (OnCatchCommandReceived?.Invoke(chatter) ?? Task.CompletedTask);
                        _battleService.Attackers.Add(trainer.Name);
                        _battleService.IsAttacking = true;

                        string? pokemonName = args?.Length == 1 ? args[0] : null;
                        if (!string.IsNullOrWhiteSpace(pokemonName))
                        {
                            // attack with the pokemon specified
                            var requestedMon = new Pokemon { Name = pokemonName };
                            bool hasPokemon = await _trainerService.HasPokemon(trainer, requestedMon);
                            if (!hasPokemon)
                            {
                                if (pokemonName == "" || pokemonName is null)
                                {
                                    SendAlert($"{chatter.Name}, please try !catchattack again. (duplicate message detected)");
                                } else
                                {
                                    SendAlert($"{chatter.Name}, you don't have the Pokémon {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pokemonName)}!");
                                }
                                _battleService.Attackers.Remove(chatter.Name);
                                break;
                            }

                            bool isShiny = await _trainerService.IsShiny(trainer, requestedMon);
                            var attacker = await _trainerService.GetPokemonFromDb(pokemonName, trainer, isShiny);
                            if (attacker == null)
                            {
                                break;
                            }

                            await OnChatUpdateMessage($"{trainer.Name} sent out {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pokemonName)}!");
                            ChangeSprite(attacker);
                            _battleService.AttackPokemon(attacker, cs_currentPokemon);
                        }
                        else
                        {
                            // attack with a random pokemon
                            var randomMon = trainer.CaughtPokemon?.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
                            if (randomMon == null || string.IsNullOrEmpty(randomMon.Name))
                            {
                                SendAlert($"{chatter.Name}, you don't have any valid Pokémon!");
                                _battleService.Attackers.Remove(chatter.Name);
                                break;
                            }

                            bool isShiny = await _trainerService.IsShiny(trainer, randomMon);
                            var attacker = await _trainerService.GetPokemonFromDb(randomMon.Name, trainer, isShiny);
                            if (attacker == null)
                            {
                                break;
                            }

                            await OnChatUpdateMessage($"{trainer.Name} sent out {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(randomMon.Name)}!");
                            ChangeSprite(attacker);
                            await Task.Delay(2000);
                            _battleService.AttackPokemon(attacker, cs_currentPokemon);
                        }
                        break;
                    }
                #endregion
                #region !catch
                case "!catch":
                    if (_battleService.IsRaidActive) return;
                    if (_battleService.IsPvP) return;
                    await (OnCatchCommandReceived?.Invoke(chatter) ?? Task.CompletedTask);
                    break;
                #endregion
                #region !raid
                case "!raid":
                    {
                        if (!_battleService.IsRaidActive)
                        {
                            SendAlert($"{chatter.Name}, there's no raid active right now!");
                            break;
                        }

                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await db.Trainers
                            .Include(t => t.CaughtPokemon)
                            .FirstOrDefaultAsync(t => t.Name == chatter.Name);

                        if (trainer == null || trainer.CaughtPokemon == null || !trainer.CaughtPokemon.Any())
                        {
                            SendAlert($"{chatter.Name}, you need a trainer profile and at least one Pokémon to join the raid!");
                            break;
                        }

                        if (string.IsNullOrEmpty(trainer.Name)) break;

                        if (_battleService.Attackers.Contains(trainer.Name))
                        {
                            SendAlert($"{chatter.Name}, you're already in the raid!");
                            break;
                        }

                        _battleService.Attackers.Add(trainer.Name);
                        SendAlert($"{chatter.Name} joined the raid with their team!");
                        await RunRaidBattle(trainer);
                        break;
                    }
                #endregion
                #region !release
                case "!release":
                    if (args?.Length == 1)
                    {
                        string pokemonName = args[0];
                        if (cs_currentPokemon is not null) break;

                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == chatter.Name);
                        if (trainer is null)
                        {
                            SendAlert($"{chatter.Name}, you don't have a trainer profile!");
                            break;
                        }
                        var pokemon = new Pokemon { Name = pokemonName };
                        if (pokemonName is null) break;

                        bool hasPokemon = await _trainerService.HasPokemon(trainer, pokemon);
                        if (!hasPokemon)
                        {
                            SendAlert($"{chatter.Name}, you don't have the Pokémon {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pokemonName)}!");
                            break;
                        }

                        var caughtEntry = await db.CaughtPokemon.FirstOrDefaultAsync(cp => cp.TrainerId == trainer.Id && cp.Name == pokemon.Name);

                        if (caughtEntry != null)
                        {
                            SendAlert($"{trainer.Name} released {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pokemon.Name)}!");
                            db.CaughtPokemon.Remove(caughtEntry);
                            await db.SaveChangesAsync();
                        }
                        else
                        {
                            SendAlert($"{chatter.Name}, something went wrong — couldn't find that Pokémon to release.");
                            break;
                        }

                        bool teamChanged = false;

                        if (trainer.Team1 == pokemon.Name) { trainer.Team1 = null; teamChanged = true; }
                        else if (trainer.Team2 == pokemon.Name) { trainer.Team2 = null; teamChanged = true; }
                        else if (trainer.Team3 == pokemon.Name) { trainer.Team3 = null; teamChanged = true; }
                        else if (trainer.Team4 == pokemon.Name) { trainer.Team4 = null; teamChanged = true; }
                        else if (trainer.Team5 == pokemon.Name) { trainer.Team5 = null; teamChanged = true; }
                        else if (trainer.Team6 == pokemon.Name) { trainer.Team6 = null; teamChanged = true; }

                        if (teamChanged)
                        {
                            db.Trainers.Update(trainer);
                        }

                        await db.SaveChangesAsync();
                    }
                    break;
                #endregion
                #region !throw
                case "!throw":
                    if (ThrowOutPokemon is not null)
                    {
                        if (args?.Length == 1)
                        {
                            string pokemonName = args[0];
                            if (cs_currentPokemon is not null) break;

                            await using var db = await _dbContextFactory.CreateDbContextAsync();
                            var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == chatter.Name);
                            if (trainer is null)
                            {
                                SendAlert($"{chatter.Name}, you don't have a trainer profile!");
                                break;
                            }
                            var pokemon = new Pokemon { Name = pokemonName };
                            if (pokemonName is null) break;

                            bool hasPokemon = await _trainerService.HasPokemon(trainer, pokemon);
                            if (!hasPokemon)
                            {
                                SendAlert($"{chatter.Name}, you don't have the Pokémon {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pokemonName)}!");
                                break;
                            }

                            var caughtEntry = await db.CaughtPokemon.FirstOrDefaultAsync(cp => cp.TrainerId == trainer.Id && cp.Name == pokemon.Name);

                            if (caughtEntry != null)
                            {
                                db.CaughtPokemon.Remove(caughtEntry);
                                await db.SaveChangesAsync();
                            }
                            else
                            {
                                SendAlert($"{chatter.Name}, something went wrong — couldn't find that Pokémon to release.");
                                break;
                            }

                            bool teamChanged = false;

                            if (trainer.Team1 == pokemon.Name) { trainer.Team1 = null; teamChanged = true; }
                            else if (trainer.Team2 == pokemon.Name) { trainer.Team2 = null; teamChanged = true; }
                            else if (trainer.Team3 == pokemon.Name) { trainer.Team3 = null; teamChanged = true; }
                            else if (trainer.Team4 == pokemon.Name) { trainer.Team4 = null; teamChanged = true; }
                            else if (trainer.Team5 == pokemon.Name) { trainer.Team5 = null; teamChanged = true; }
                            else if (trainer.Team6 == pokemon.Name) { trainer.Team6 = null; teamChanged = true; }

                            if (teamChanged)
                            {
                                db.Trainers.Update(trainer);
                            }

                            await db.SaveChangesAsync();

                            await OnChatUpdateMessage($"{trainer.Name} released {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pokemon.Name)} back into the wild!");
                            await ThrowOutPokemon.Invoke(caughtEntry, false);
                        }
                    }
                    break;
                #endregion
                #region !challenge
                case "!challenge":
                    {
                        if (args?.Length != 1)
                        {
                            SendAlert($"{chatter.Name}, usage: !challenge [opponent name]");
                            break;
                        }

                        string opponentName = args[0];
                        if (string.Equals(opponentName, chatter.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            SendAlert($"{chatter.Name}, you can't challenge yourself!");
                            break;
                        }

                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var challenger = await db.Trainers.FirstOrDefaultAsync(t => t.Name == chatter.Name);
                        var opponent = await db.Trainers.FirstOrDefaultAsync(t => t.Name == opponentName);

                        if (challenger == null || opponent == null)
                        {
                            SendAlert($"{chatter.Name}, one or both trainers were not found.");
                            break;
                        }

                        _battleService.IsPvP = true;
                        _battleService.PvPChallenger = challenger;
                        _battleService.PvPOpponent = opponent;

                        SendAlert($"{chatter.Name} has challenged {opponentName} to a Pokémon battle!");

                        var opponentTeam = new[] { opponent.Team1, opponent.Team2, opponent.Team3, opponent.Team4, opponent.Team5, opponent.Team6 };
                        string? firstMonName = opponentTeam.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
                        if (firstMonName == null)
                        {
                            SendAlert($"{opponentName} has no valid Pokémon on their team!");
                            break;
                        }

                        bool isShiny = await _trainerService.IsShinyByName(opponent, firstMonName);
                        var opponentMon = await _trainerService.GetPokemonFromDb(firstMonName, opponent, isShiny);
                        if (opponentMon == null)
                        {
                            SendAlert($"{opponentName}'s Pokémon {firstMonName} could not be loaded!");
                            break;
                        }
                        await RunPvPBattle();
                        break;
                    }
                #endregion
                #region !stats
                case "!stats":
                    if (args?.Length == 1)
                    {
                        Pokemon? p = await Pokedex.GetPokemonFromDbByNameAsync(args[0].ToLower());
                        if (p == null || p.ToString() == string.Empty)
                        {
                            SendAlert($"Look up failed! Could not find a Pokemon named: {args[0].ToLower()}");
                            break;
                        }
                        SendAlert(p.ToString());
                    }
                    break;
                #endregion
                #region !team
                case "!team":
                    {
                        if (args?.Length == 1)
                        {
                            await using var db = await _dbContextFactory.CreateDbContextAsync();
                            var trainer = await db.Trainers
                                .FirstOrDefaultAsync(t => t.Name.ToLower() == args[0].ToLower());
                            if (trainer is not null && !string.IsNullOrEmpty(trainer.Name))
                            {
                                // only print however many string's have been set to a pokemon name
                                var team = new[] { trainer.Team1, trainer.Team2, trainer.Team3, trainer.Team4, trainer.Team5, trainer.Team6 };

                                var result = new List<string>();
                                foreach (var mon in team)
                                {
                                    if (string.IsNullOrEmpty(mon)) continue;
                                    result.Add(mon);
                                }

                                SendAlert($"{trainer.Name}'s Team - {string.Join(", ", result)}");
                            }
                            else
                            {
                                SendAlert($"{chatter.Name}, {args[0]} does not have a team set!");
                            }
                        }
                        else
                        {
                            await using var db = await _dbContextFactory.CreateDbContextAsync();
                            
                            var trainer = await db.Trainers
                                .FirstOrDefaultAsync(t => t.Name == chatter.Name);
                            if (trainer != null && !string.IsNullOrEmpty(trainer.Name))
                            {
                                // only print however many string's have been set to a pokemon name
                                var team = new[] { trainer.Team1, trainer.Team2, trainer.Team3, trainer.Team4, trainer.Team5, trainer.Team6 };

                                var result = new List<string>();
                                foreach (var mon in team)
                                {
                                    if (string.IsNullOrEmpty(mon)) continue;
                                    result.Add(mon);
                                }

                                SendAlert($"{trainer.Name}'s Team - {string.Join(", ", result)}");
                            }
                            else
                            {
                                SendAlert($"{chatter.Name}, you don't have a team set!");
                            }
                        }
                    }
                    break;
                #endregion
                #region !changeteam
                case "!changeteam":
                    {
                        if (args?.Length == 0 || args == null)
                        {
                            SendAlert("Please provide up to 6 Pokémon names separated by commas.");
                            break;
                        }
                        var input = string.Join(' ', args);
                        var requestedTeam = input.Split(',')
                            .Select(p => p.Trim())
                            .Where(p => !string.IsNullOrEmpty(p))
                            .Take(6)
                            .ToList();

                        if (requestedTeam.Count == 0)
                        {
                            SendAlert("No valid Pokémon names detected.");
                            break;
                        }

                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == chatter.Name);

                        if (trainer == null)
                        {
                            SendAlert($"Trainer {chatter.Name} not found.");
                            break;
                        }

                        var newTeam = new List<string>();

                        foreach (var name in requestedTeam)
                        {
                            var pokemon = await _trainerService.GetPokemonFromDb(name, trainer);
                            if (pokemon == null || !await _trainerService.HasPokemon(trainer, pokemon))
                            {
                                SendAlert($"{chatter.Name} does not have a {name}!");
                                break;
                            }

                            newTeam.Add(name);
                        }

                        if (newTeam.Count == requestedTeam.Count)
                        {
                            trainer.Team1 = newTeam.ElementAtOrDefault(0);
                            trainer.Team2 = newTeam.ElementAtOrDefault(1);
                            trainer.Team3 = newTeam.ElementAtOrDefault(2);
                            trainer.Team4 = newTeam.ElementAtOrDefault(3);
                            trainer.Team5 = newTeam.ElementAtOrDefault(4);
                            trainer.Team6 = newTeam.ElementAtOrDefault(5);

                            db.Trainers.Update(trainer);
                            await db.SaveChangesAsync();

                            SendAlert($"Team updated: {string.Join(", ", newTeam)}");
                        }
                    }
                    break;
                #endregion
                #region !attack
                case "!attack":
                    if (_battleService.IsRaidActive) return;
                    if (_battleService.IsPvP) return;
                    if (args?.Length == 1)
                    {
                        if (_battleService.Attackers.Exists(name => name == chatter.Name))
                        {
                            return;
                        }
                        if (_battleService.Attackers.Contains(chatter.Name)) return;
                        string pokemonName = args[0];
                        if (cs_currentPokemon == null)
                        {
                            break;
                        }
                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == chatter.Name);
                        if (trainer == null)
                        {
                            SendAlert($"{chatter.Name}, you don't have a trainer profile!");
                            _battleService.Attackers.Remove(chatter.Name);
                            break;
                        }
                        var pokemon = new Pokemon { Name = pokemonName };
                        if (pokemonName is null) break;
                        bool hasPokemon = await _trainerService.HasPokemon(trainer, pokemon);
                        if (!hasPokemon)
                        {
                            SendAlert($"{chatter.Name}, you don't have the Pokémon {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pokemonName)}!");
                            _battleService.Attackers.Remove(chatter.Name);
                            break;
                        }
                        await OnChatUpdateMessage($"{trainer.Name} sent out {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pokemon.Name)}!");
                        bool isShiny = await _trainerService.IsShiny(trainer, pokemon);
                        var attacker = await Pokedex.GetPokemonFromDbByNameAsync(pokemonName, isShiny);
                        if (attacker == null)
                        {
                            break;
                        }
                        ChangeSprite(attacker);
                        if (!string.IsNullOrEmpty(trainer.Name))
                        {
                            _battleService.Attackers.Add(trainer.Name);
                        }
                        await Task.Delay(2000);
                        _battleService.AttackPokemon(attacker, cs_currentPokemon);
                    }
                    else
                    {
                        if (_battleService.Attackers.Exists(name => name == chatter.Name))
                        {
                            return;
                        }
                        if (_battleService.Attackers.Contains(chatter.Name)) return;

                        if (cs_currentPokemon == null)
                        {
                            break;
                        }
                        await using var db = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await db.Trainers
                            .FirstOrDefaultAsync(t => t.Name == chatter.Name);
                        if (trainer == null)
                        {
                            SendAlert($"{chatter.Name}, you don't have a trainer profile!");
                            _battleService.Attackers.Remove(chatter.Name);
                            break;
                        }

                        if (!string.IsNullOrEmpty(trainer.Name))
                        {
                            _battleService.Attackers.Add(trainer.Name);
                            _battleService.IsAttacking = true;
                        }
                        if (trainer == null) break;
                        var trainerName = _battleService.Attackers[0];
                        var currentTrainer = await db.Trainers
                            .Include(t => t.CaughtPokemon)
                            .FirstOrDefaultAsync(t => t.Name == chatter.Name);
                        if (currentTrainer == null)
                        {
                            return;
                        }

                        var pokemon = trainer.CaughtPokemon?.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
                        if (pokemon == null || string.IsNullOrEmpty(pokemon.Name))
                        {
                            return;
                        }

                        await OnChatUpdateMessage($"{trainer.Name} sent out {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pokemon.Name)}!");

                        bool IsShiny = await _trainerService.IsShiny(trainer, pokemon);
                        var attacker = await _trainerService.GetPokemonFromDb(pokemon.Name, trainer, IsShiny);
                        if (attacker == null)
                        {
                            return;
                        }

                        ChangeSprite(attacker);
                        await Task.Delay(2000);
                        _battleService.AttackPokemon(attacker, cs_currentPokemon);
                    }
                    break;
                #endregion
                #region !changetrainer
                case "!changetrainer":
                    if (args?.Length == 1 && int.TryParse(args[0], out int trainerIndex))
                    {
                        if (trainerIndex < 0 || trainerIndex > 120)
                        {
                            SendAlert($"{chatter.Name}, invalid trainer number! Choose between 0 and 119.");
                            break;
                        }

                        await using var Database = await _dbContextFactory.CreateDbContextAsync();
                        var trainer = await Database.Trainers.FirstOrDefaultAsync(t => t.Name == chatter.Name);
                        if (trainer == null)
                        {
                            trainer = new Trainer { Name = chatter.Name, SpriteIndex = trainerIndex };
                            Database.Trainers.Add(trainer);
                        }
                        else
                        {
                            trainer.SpriteIndex = trainerIndex;
                        }
                        await Database.SaveChangesAsync();

                        SendAlert($"{chatter.Name} changed their trainer to sprite #{trainerIndex}!");
                    }
                    else
                    {
                        SendAlert($"{chatter.Name}, usage: !changetrainer [0-103]");
                    }
                    break;
                #endregion
                default:
                    break;
            }
            await SafeInvokeChatUpdated();
        }
    }
}
