using Discord;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using DotNetEnv;

class Program {
     private static IServiceProvider? ServiceProvider;

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

          ServiceProvider = new ServiceCollection()
               .AddSingleton<DiscordSocketClient>()
               .AddSingleton<DiscordWebhookClient>(_ => new DiscordWebhookClient(LOG_WEBHOOK_URL))
               .AddSingleton<ILogger, ComboLogger>()
               .AddSingleton<InteractionService>(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>().Rest, null))
               .AddSingleton<InteractionHandler>()
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

     private static Task SlashCommandHandler(SocketSlashCommand command) {
          switch (command.CommandName) {
               case "ping":
                    command.RespondAsync($"Command ID: {command.CommandId}, Command Name: {command.CommandName}");
                    break;
               default:
                    command.RespondAsync($"Unknown Command????? HOW ?");
                    Console.WriteLine($"Unknown Command: {command.CommandName}");
                    break;
          }
          return Task.CompletedTask;
     }
}