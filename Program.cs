using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using DotNetEnv;
using Serilog;
using Serilog.Enrichers.CallerInfo;

class Program {
     private static IServiceProvider? ServiceProvider;

     private static readonly DiscordSocketConfig SocketConfig = new()
     {
          GatewayIntents = GatewayIntents.GuildVoiceStates | GatewayIntents.GuildMembers | GatewayIntents.Guilds,
          AlwaysDownloadUsers = true,
          LogLevel = LogSeverity.Warning,
     };

     private static readonly InteractionServiceConfig ServiceConfig = new() {
          LogLevel = LogSeverity.Debug,
     };

     public static async Task Main(string[] args) {
          Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .Enrich.WithCallerInfo(includeFileInfo: true, assemblyPrefix: "dotnetDiscordBot", filePathDepth: 3)
               .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}][{Method}] {Message}{NewLine}{Exception}")
               .CreateLogger();

          // Check and Set Env variables.
          Env.Load();
          string? DISCORD_TOKEN = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
          if (string.IsNullOrEmpty(DISCORD_TOKEN)) {
               Log.Fatal("Failed to acquire DISCORD_TOKEN as an environment variable");
               return;
          }
          string? LOG_WEBHOOK_URL = Environment.GetEnvironmentVariable("LOG_WEBHOOK_URL");
          if (string.IsNullOrEmpty(LOG_WEBHOOK_URL)) {
               Log.Fatal("Failed to acquire LOG_WEBHOOK_URL as an environment variable");
               return;
          }

          // Setup Dependency Injection and Initialize Services
          ServiceProvider = new ServiceCollection()
               .AddSingleton<DiscordSocketClient>(_ => new DiscordSocketClient(SocketConfig))
               .AddSingleton<YTAPIManager>(x => new YTAPIManager())
               .AddSingleton<YTSearchAutocomplete>()
               .AddSingleton<InteractionService>(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>().Rest, ServiceConfig))
               .AddSingleton<InteractionHandler>()
               .AddSingleton<ConcurrentDictionary<ulong, GuildData>>()
               .BuildServiceProvider();

          DiscordSocketClient SocketClient = ServiceProvider.GetRequiredService<DiscordSocketClient>();

          // Initialize Interaction Handler
          await ServiceProvider.GetRequiredService<InteractionHandler>().InitializeAsync();

          // Connect to Discord Gateway
          await SocketClient.LoginAsync(TokenType.Bot, DISCORD_TOKEN);
          await SocketClient.StartAsync();

          await Task.Delay(Timeout.Infinite);
     }
}