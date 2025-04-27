using VeCatch.Components;
using VeCatch.Services;
using VeCatch.Models;
using Microsoft.EntityFrameworkCore;

// todo: add twitch login via console here

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options =>
    
     {
         options.DetailedErrors = true;
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
    string channelName = authService.GetChannelName();
    var dbContextFactory = sp.GetRequiredService<IDbContextFactory<DatabaseInfo>>();
    var trainerService = sp.GetRequiredService<TrainerService>();
    var battleService = sp.GetRequiredService<BattleService>();
    return new ChatService(channelName, dbContextFactory, trainerService, battleService);
});
builder.Services.AddSingleton(sp =>
{
    var authService = sp.GetRequiredService<AuthService>();
    var battleService = sp.GetRequiredService<BattleService>();
    var dbContextFactory = sp.GetRequiredService<IDbContextFactory<DatabaseInfo>>();
    var chatService = sp.GetRequiredService<ChatService>();
    return new PokemonService(dbContextFactory, chatService, battleService, authService);
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days.
    // You may want to change this for production scenarios
    // see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();