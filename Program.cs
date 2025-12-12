using VeCatch.Components;
using VeCatch.Services;
using VeCatch.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components;
using System.Reflection;

// todo: add twitch login via console here

var builder = WebApplication.CreateBuilder(args);
//builder.WebHost.UseUrls("https://localhost:7002");

//builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options =>
    
     {
         // debug only options.DetailedErrors = true;
         options.MaxBufferedUnacknowledgedRenderBatches = 50;
         options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(5);
     });

builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton(rp =>
{
    var dbContextFactory = rp.GetRequiredService<IDbContextFactory<DatabaseInfo>>();
    var chatService = rp.GetRequiredService<ChatService>();
    var trainerService = rp.GetRequiredService<TrainerService>();
    return new RedeemService(dbContextFactory, chatService, trainerService);
});
builder.Services.AddScoped<CatchService>();
builder.Services.AddSingleton<BattleService>();
builder.Services.AddSingleton<TrainerService>();
builder.Services.AddSingleton(sp =>
{
    var authService = sp.GetRequiredService<AuthService>();
    var battleService = sp.GetRequiredService<BattleService>();
    var dbContextFactory = sp.GetRequiredService<IDbContextFactory<DatabaseInfo>>();
    return new PokemonService(dbContextFactory, battleService, authService);
});
builder.Services.AddSingleton(sp =>
{
    var authService = sp.GetRequiredService<AuthService>();
    string channelName = authService.GetChannelName();
    var dbContextFactory = sp.GetRequiredService<IDbContextFactory<DatabaseInfo>>();
    var trainerService = sp.GetRequiredService<TrainerService>();
    var battleService = sp.GetRequiredService<BattleService>();
    var pokemonService = sp.GetRequiredService<PokemonService>();
    return new ChatService(channelName, dbContextFactory, trainerService, battleService, pokemonService);
});
builder.Services.AddSingleton<ActivityStateService>();

builder.Services.AddDbContextFactory<DatabaseInfo>((sp, options) =>
{
    var authService = sp.GetRequiredService<AuthService>();
    string channelName = authService.GetChannelName();
    options.UseSqlite($"Data Source=\"{channelName}.db\";Cache=Shared");
});
builder.Services.AddDbContextFactory<PokemonDatabase>(options =>
    options.UseSqlite("Data Source=pokemon.db"));
builder.Services.AddHttpClient();
var app = builder.Build();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PokemonDatabase>>().CreateDbContext();
await db.Database.EnsureCreatedAsync();

/* localhost app -- this is not needed (i think?)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days.
    // You may want to change this for production scenarios
    // see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
*/

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Console.Title = $"VeCatch v{Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}";
Console.Write("Browser Source URL: ");
Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("http://localhost:5000");
Console.ResetColor();

app.Run();