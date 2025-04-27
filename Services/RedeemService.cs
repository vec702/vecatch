using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VeCatch.Models;

namespace VeCatch.Services
{
    [ApiController]
    [Route("twitch/events")]
    public class RedeemService : ControllerBase
    {
        private readonly IDbContextFactory<DatabaseInfo> _dbContextFactory;
        private readonly ChatService _chatService;
        private readonly TrainerService _trainerService;
        public event Func<Task>? RandomPkmnRedeem;

        public RedeemService(IDbContextFactory<DatabaseInfo> dbContextFactory, ChatService chatService, TrainerService trainerService)
        {
            _dbContextFactory = dbContextFactory;
            _chatService = chatService;
            _trainerService = trainerService;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveTwitchEvent([FromBody] TwitchEventPayload payload)
        {
            if(payload == null)
            {
                return BadRequest(payload);
            }
            if (payload?.Subscription?.Type == "channel.channel_points_custom_reward_redemption.add")
            {
                string? rewardName = payload?.Event?.Reward?.Title;
                string? user = payload?.Event?.UserName;
                if(rewardName is not null && user is not null)
                {
                    await HandleRedemptionAsync(rewardName, user);
                }
            }
            return Ok();
        }

        private async Task HandleRedemptionAsync(string rewardName, string user)
        {
            switch(rewardName)
            {
                #region Buy Ultra Ball
                case "Buy Ultra Ball":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{user} triggered the \"Buy Ultra Ball\" redeem!");
                    Console.ResetColor();
                    var db = await _dbContextFactory.CreateDbContextAsync();
                    var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Name == user);
                    if (trainer is not null)
                    {
                        trainer.UltraBalls += 1;
                        await _trainerService.UpdateTrainer(trainer);
                        _chatService.SendAlert($"{trainer.Name} received an Ultra Ball! They have: {trainer.UltraBalls}.");
                    }
                    break;
                #endregion
                #region Lure Pokemon
                case "Lure Pokémon":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{user} triggered the \"Lure Pokémon\" redeem!");
                    Console.ResetColor();
                    if (RandomPkmnRedeem is not null)
                    {
                        await RandomPkmnRedeem.Invoke();
                        _chatService.SendAlert($"{user} used a Poké Lure!");
                    }
                    break;
                #endregion
            }
            await Task.CompletedTask;
        }
    }

    public class TwitchEventPayload
    {
        public TwitchSubscription? Subscription { get; set; }
        public TwitchEvent? Event { get; set; }
    }

    public class TwitchSubscription
    {
        public string? Type { get; set; }
    }

    public class TwitchEvent
    {
        public string? UserName { get; set; }
        public TwitchReward? Reward { get; set; }
    }

    public class TwitchReward
    {
        public string? Title { get; set; }
    }
}
