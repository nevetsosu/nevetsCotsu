using Discord;
using Discord.WebSocket;
using Discord.Webhook;
using DotNetEnv;

class Program {
     private static DiscordSocketClient? SocketClient;
     private static DiscordWebhookClient? WebhookClient;

     public static async Task Main(string[] args) {
          // Check and Set Env variables.
          Env.Load();
          string? DISCORD_TOKEN = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
          if (string.IsNullOrEmpty(DISCORD_TOKEN)) {
               Console.Error.WriteLine("[FATAL] Failed to acquire DISCORD_TOKEN as an environment variable");
               return;
          }

          string? LOG_WEBHOOK_URL = Environment.GetEnvironmentVariable("LOG_WEBHOOK_URL");
          if (string.IsNullOrEmpty(DISCORD_TOKEN)) {
               Console.Error.WriteLine("[FATAL] Failed to acquire LOG_WEBHOOK_URL as an environment variable");
               return;
          }

          // Create and Validate WebhookClient
          WebhookClient = new DiscordWebhookClient(LOG_WEBHOOK_URL);
          if (WebhookClient == null) {
               Console.Error.WriteLine("[WARN] Failed to connect Webhook");
          }

          // Create and Validate SocketClient
          SocketClient = new DiscordSocketClient();
          if (SocketClient == null) {
               Console.Error.WriteLine("[FATAL] DiscordSocketClient returned null");
               return;
          };

          // Add Callbacks
          SocketClient.Log += LogAsync;
          SocketClient.Ready += ReadyAsync;
          SocketClient.SlashCommandExecuted += SlashCommandHandler;

          // Connect to Discord Gateway
          await SocketClient.LoginAsync(TokenType.Bot, DISCORD_TOKEN);
          await SocketClient.StartAsync();

          await Task.Delay(-1);
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

     private static Task ReadyAsync() {
          // Register a Ping-Pong Command
          var command = new SlashCommandBuilder();
          command.WithName("ping");
          command.WithDescription("Responds with pong!");

          if (SocketClient != null) SocketClient.CreateGlobalApplicationCommandAsync(command.Build());
          return Task.CompletedTask;
     }

     private static async Task LogAsync(LogMessage message) {
          Console.WriteLine($"[General/{message.Severity}] {message}");

          if (WebhookClient != null) await WebhookClient.SendMessageAsync($"```[nevetsCotsu] {message.ToString()}```");
     }
}