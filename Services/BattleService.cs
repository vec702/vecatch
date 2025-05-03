using Microsoft.AspNetCore.Components;
using System.Globalization;
using VeCatch.Models;

namespace VeCatch.Services
{
    public class BattleService
    {
        public List<string> Attackers = new List<string>();
        public bool IsRaidActive { get; private set; } = false;
        public bool IsAttacking = false;
        public bool IsPvP { get; set; } = false;
        public Trainer? PvPChallenger { get; set; }
        public Trainer? PvPOpponent { get; set; }
        public event Func<MarkupString, Task>? UpdateMessage;
        public async Task OnUpdateMessage(string s)
        {
            if(UpdateMessage != null)
            {
                string message = $"\n{s}";
                await UpdateMessage.Invoke(new MarkupString($"{message.Replace("\n", "<br />")}"));
                await Task.Delay(3000);
                await UpdateMessage.Invoke(new MarkupString(""));
            }
        }

        public void StartRaid()
        {
            IsRaidActive = true;
        }

        public void EndRaid()
        {
            IsRaidActive = false;
        }

        public async void AttackPokemon(Pokemon Attacker, Pokemon Target)
        {
            if (Attacker == null || Target == null) return;
            Random random = new Random();

            bool isPhysical = Attacker.Attack >= Attacker.SpecialAttack;
            int attackStat = isPhysical ? Attacker.Attack : Attacker.SpecialAttack;
            int defenseStat = isPhysical ? Target.Defense : Target.SpecialDefense;

            double baseDamage = (((2 * Attacker.Level / 5.0 + 2) * attackStat * 50 / defenseStat) / 50) + 2;

            bool isCritical = random.Next(16) == 0;
            double criticalMultiplier = isCritical ? 1.5 : 1.0;

            double type1Multiplier = GetTypeEffectiveness(Attacker.Type1.ToString(), Target.Type1.ToString());
            double type2Multiplier = Target.Type2 != PokemonType.None ? GetTypeEffectiveness(Attacker.Type1.ToString(), Target.Type2.ToString()) : 1.0;
            double totalTypeMultiplier = type1Multiplier * type2Multiplier;

            double randomMultiplier = (random.Next(85, 101) / 100.0);

            int finalDamage = (int)(baseDamage * criticalMultiplier * totalTypeMultiplier * randomMultiplier);

            Target.TakeDamage(finalDamage);

            if (isCritical)
            {
                await OnUpdateMessage($"🔥 Critical hit! 🔥 {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Attacker.Name)} deals {finalDamage} damage!");
            }

            if (totalTypeMultiplier > 1.0)
            {
                await OnUpdateMessage($"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Attacker.Name)} deals {finalDamage} damage! It's super effective!");
            }
            else if (totalTypeMultiplier < 1.0)
            {
                await OnUpdateMessage($"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Attacker.Name)} deals {finalDamage} damage! It's not very effective...");
            }
            else if (!isCritical && totalTypeMultiplier == 1.0)
            {
                await OnUpdateMessage($"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Attacker.Name)} deals {finalDamage} damage!");
            }

            IsAttacking = false;
        }

        private double GetTypeEffectiveness(string attackerType, string targetType)
        {
            var effectivenessChart = new Dictionary<string, Dictionary<string, double>>
            {
                { "Fire", new Dictionary<string, double> {
                    { "Fire", 0.5 }, { "Water", 0.5 }, { "Grass", 2.0 }, { "Electric", 1.0 }, { "Bug", 2.0 },
                    { "Fairy", 1.0 }, { "Fighting", 1.0 }, { "Flying", 1.0 }, { "Ghost", 1.0 }, { "Ground", 1.0 },
                    { "Ice", 2.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 0.5 },
                    { "Steel", 2.0 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Water", new Dictionary<string, double> {
                    { "Fire", 2.0 }, { "Water", 0.5 }, { "Grass", 0.5 }, { "Electric", 1.0 }, { "Bug", 1.0 },
                    { "Fairy", 1.0 }, { "Fighting", 1.0 }, { "Flying", 1.0 }, { "Ghost", 1.0 }, { "Ground", 2.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 1.0 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Grass", new Dictionary<string, double> {
                    { "Fire", 0.5 }, { "Water", 2.0 }, { "Grass", 0.5 }, { "Electric", 1.0 }, { "Bug", 0.5 },
                    { "Fairy", 1.0 }, { "Fighting", 1.0 }, { "Flying", 0.5 }, { "Ghost", 1.0 }, { "Ground", 2.0 },
                    { "Ice", 0.5 }, { "Normal", 1.0 }, { "Poison", 0.5 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 1.0 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Electric", new Dictionary<string, double> {
                    { "Fire", 1.0 }, { "Water", 2.0 }, { "Grass", 1.0 }, { "Electric", 0.5 }, { "Bug", 1.0 },
                    { "Fairy", 1.0 }, { "Fighting", 1.0 }, { "Flying", 2.0 }, { "Ghost", 1.0 }, { "Ground", 0.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 1.0 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Bug", new Dictionary<string, double> {
                    { "Fire", 0.5 }, { "Water", 1.0 }, { "Grass", 2.0 }, { "Electric", 1.0 }, { "Bug", 1.0 },
                    { "Fairy", 0.5 }, { "Fighting", 1.0 }, { "Flying", 0.5 }, { "Ghost", 1.0 }, { "Ground", 1.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 2.0 }, { "Psychic", 1.0 }, { "Rock", 2.0 },
                    { "Steel", 1.0 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Fairy", new Dictionary<string, double> {
                    { "Fire", 0.5 }, { "Water", 1.0 }, { "Grass", 1.0 }, { "Electric", 1.0 }, { "Bug", 0.5 },
                    { "Fairy", 1.0 }, { "Fighting", 2.0 }, { "Flying", 1.0 }, { "Ghost", 1.0 }, { "Ground", 1.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 0.5 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 0.5 }, { "Dragon", 2.0 }, { "Dark", 2.0 }
                }},
                { "Fighting", new Dictionary<string, double> {
                    { "Fire", 1.0 }, { "Water", 1.0 }, { "Grass", 1.0 }, { "Electric", 1.0 }, { "Bug", 1.0 },
                    { "Fairy", 0.5 }, { "Fighting", 1.0 }, { "Flying", 0.5 }, { "Ghost", 0.0 }, { "Ground", 1.0 },
                    { "Ice", 2.0 }, { "Normal", 2.0 }, { "Poison", 1.0 }, { "Psychic", 0.5 }, { "Rock", 2.0 },
                    { "Steel", 2.0 }, { "Dragon", 1.0 }, { "Dark", 2.0 }
                }},
                { "Flying", new Dictionary<string, double> {
                    { "Fire", 1.0 }, { "Water", 1.0 }, { "Grass", 2.0 }, { "Electric", 0.5 }, { "Bug", 2.0 },
                    { "Fairy", 1.0 }, { "Fighting", 2.0 }, { "Flying", 1.0 }, { "Ghost", 1.0 }, { "Ground", 1.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 1.0 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Ghost", new Dictionary<string, double> {
                    { "Fire", 1.0 }, { "Water", 1.0 }, { "Grass", 1.0 }, { "Electric", 1.0 }, { "Bug", 1.0 },
                    { "Fairy", 1.0 }, { "Fighting", 0.0 }, { "Flying", 1.0 }, { "Ghost", 2.0 }, { "Ground", 1.0 },
                    { "Ice", 1.0 }, { "Normal", 0.5 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 1.0 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Ground", new Dictionary<string, double> {
                    { "Fire", 2.0 }, { "Water", 1.0 }, { "Grass", 0.5 }, { "Electric", 2.0 }, { "Bug", 1.0 },
                    { "Fairy", 1.0 }, { "Fighting", 1.0 }, { "Flying", 0.0 }, { "Ghost", 1.0 }, { "Ground", 1.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 2.0 }, { "Psychic", 1.0 }, { "Rock", 2.0 },
                    { "Steel", 2.0 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Ice", new Dictionary<string, double> {
                    { "Fire", 0.5 }, { "Water", 0.5 }, { "Grass", 2.0 }, { "Electric", 1.0 }, { "Bug", 1.0 },
                    { "Fairy", 1.0 }, { "Fighting", 1.0 }, { "Flying", 2.0 }, { "Ghost", 1.0 }, { "Ground", 2.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 0.5 }, { "Dragon", 2.0 }, { "Dark", 1.0 }
                }},
                { "Normal", new Dictionary<string, double> {
                    { "Fire", 1.0 }, { "Water", 1.0 }, { "Grass", 1.0 }, { "Electric", 1.0 }, { "Bug", 1.0 },
                    { "Fairy", 1.0 }, { "Fighting", 1.0 }, { "Flying", 1.0 }, { "Ghost", 0.0 }, { "Ground", 1.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 1.0 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Poison", new Dictionary<string, double> {
                    { "Fire", 1.0 }, { "Water", 1.0 }, { "Grass", 2.0 }, { "Electric", 1.0 }, { "Bug", 1.0 },
                    { "Fairy", 2.0 }, { "Fighting", 1.0 }, { "Flying", 1.0 }, { "Ghost", 1.0 }, { "Ground", 2.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 0.5 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 0.5 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Psychic", new Dictionary<string, double> {
                    { "Fire", 1.0 }, { "Water", 1.0 }, { "Grass", 1.0 }, { "Electric", 1.0 }, { "Bug", 1.0 },
                    { "Fairy", 1.0 }, { "Fighting", 2.0 }, { "Flying", 1.0 }, { "Ghost", 2.0 }, { "Ground", 1.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 0.5 }, { "Dragon", 1.0 }, { "Dark", 0.5 }
                }},
                { "Rock", new Dictionary<string, double> {
                    { "Fire", 2.0 }, { "Water", 1.0 }, { "Grass", 1.0 }, { "Electric", 1.0 }, { "Bug", 2.0 },
                    { "Fairy", 1.0 }, { "Fighting", 0.5 }, { "Flying", 2.0 }, { "Ghost", 1.0 }, { "Ground", 1.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 0.5 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Steel", new Dictionary<string, double> {
                    { "Fire", 0.5 }, { "Water", 0.5 }, { "Grass", 1.0 }, { "Electric", 1.0 }, { "Bug", 2.0 },
                    { "Fairy", 2.0 }, { "Fighting", 2.0 }, { "Flying", 1.0 }, { "Ghost", 1.0 }, { "Ground", 2.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 0.5 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }},
                { "Dragon", new Dictionary<string, double> {
                    { "Fire", 2.0 }, { "Water", 1.0 }, { "Grass", 1.0 }, { "Electric", 1.0 }, { "Bug", 1.0 },
                    { "Fairy", 0.5 }, { "Fighting", 1.0 }, { "Flying", 2.0 }, { "Ghost", 1.0 }, { "Ground", 1.0 },
                    { "Ice", 2.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 1.0 }, { "Rock", 1.0 },
                    { "Steel", 1.0 }, { "Dragon", 2.0 }, { "Dark", 1.0 }
                }},
                { "Dark", new Dictionary<string, double> {
                    { "Fire", 1.0 }, { "Water", 1.0 }, { "Grass", 1.0 }, { "Electric", 1.0 }, { "Bug", 1.0 },
                    { "Fairy", 0.5 }, { "Fighting", 2.0 }, { "Flying", 1.0 }, { "Ghost", 2.0 }, { "Ground", 1.0 },
                    { "Ice", 1.0 }, { "Normal", 1.0 }, { "Poison", 1.0 }, { "Psychic", 2.0 }, { "Rock", 1.0 },
                    { "Steel", 1.0 }, { "Dragon", 1.0 }, { "Dark", 1.0 }
                }}
            };

            if (effectivenessChart.ContainsKey(attackerType) && effectivenessChart[attackerType].ContainsKey(targetType))
            {
                return effectivenessChart[attackerType][targetType];
            }

            return 1.0;
        }
    }
}
