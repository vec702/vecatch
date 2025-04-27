using Microsoft.EntityFrameworkCore;
using VeCatch.Models;

namespace VeCatch.Services
{
    public class CatchService
    {
        private readonly Random _random = new();

        public int AttemptCatch(int catchRate, double healthModifier = 1.0, double ballModifier = 1.0)
        {
            double chance = Math.Min(255, catchRate * healthModifier * ballModifier);
            int shakes = 0;

            for (int i = 0; i < 4; i++)
            {
                int x = _random.Next(0, 256);
                if (x <= chance)
                {
                    shakes++;
                }
                else
                {
                    break;
                }
            }
            return shakes;
        }
    }

}
