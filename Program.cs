using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using DotNetEnv;
using Google.Apis.Services;

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
          // Check and Set Env variables.
          Env.Load();
          string? DISCORD_TOKEN = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
          if (string.IsNullOrEmpty(DISCORD_TOKEN)) {
               Console.Error.WriteLine("[FATAL] Failed to acquire DISCORD_TOKEN as an environment variable");
               return;
          }

          string? LOG_WEBHOOK_URL = Environment.GetEnvironmentVariable("LOG_WEBHOOK_URL");
          if (string.IsNullOrEmpty(LOG_WEBHOOK_URL)) {
               Console.Error.WriteLine("[FATAL] Failed to acquire LOG_WEBHOOK_URL as an environment variable");
               return;
          }

          string? YOUTUBE_API_KEY = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");
          if (string.IsNullOrEmpty(YOUTUBE_API_KEY)) {
               Console.Error.WriteLine("[FATAL] Failed to acquire YOUTUBE_API_KEY as an environment variable");
               return;
          }

          string? YOUTUBE_PROJECT_NAME = Environment.GetEnvironmentVariable("YOUTUBE_PROJECT_NAME");
          if (string.IsNullOrEmpty(YOUTUBE_PROJECT_NAME)) {
               Console.Error.WriteLine("[FATAL] Failed to acquire YOUTUBE_PROJECT_NAME as an environment variable");
               return;
          }

          ServiceProvider = new ServiceCollection()
               .AddSingleton<DiscordSocketClient>(_ => new DiscordSocketClient(SocketConfig))
               // .AddSingleton<DiscordWebhookClient>(_ => new DiscordWebhookClient(LOG_WEBHOOK_URL))
               // .AddSingleton<ILogger, ComboLogger>()
               .AddSingleton<ILogger, DefaultLogger>()
               .AddSingleton<YouTubeAPIManager>(x => new YouTubeAPIManager(YOUTUBE_API_KEY, YOUTUBE_PROJECT_NAME))
               .AddSingleton<InteractionService>(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>().Rest, ServiceConfig))
               .AddSingleton<InteractionHandler>()
               .AddSingleton<ConcurrentDictionary<ulong, GuildData>>()
               .BuildServiceProvider();

          DiscordSocketClient SocketClient = ServiceProvider.GetRequiredService<DiscordSocketClient>();

          // Enable SocketClient Logging
          SocketClient.Log += ServiceProvider.GetRequiredService<ILogger>().LogAsync;

          // Initialize Interaction Handler
          await ServiceProvider.GetRequiredService<InteractionHandler>().InitializeAsync();

          // Connect to Discord Gateway
          await SocketClient.LoginAsync(TokenType.Bot, DISCORD_TOKEN);
          await SocketClient.StartAsync();

          await Task.Delay(Timeout.Infinite);
     }
}