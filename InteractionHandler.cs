using Discord.WebSocket;
using Discord.Interactions;
using System.Reflection;

public class InteractionHandler {
     private readonly DiscordSocketClient Client;
     private readonly InteractionService Handler;
     private readonly IServiceProvider ServiceProvider;
     private readonly ILogger Logger;

     public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider serviceprovider, ILogger logger) {
          Client = client;
          Handler = handler;
          ServiceProvider = serviceprovider;
          Logger = logger;
     }

     public async Task InitializeAsync() {
          Handler.Log += Logger.LogAsync;
          Client.Ready += ReadyAsync;
          await Handler.AddModulesAsync(Assembly.GetEntryAssembly(), ServiceProvider);
     }

     private async Task ReadyAsync() {
          await Handler.RegisterCommandsGloballyAsync();
     }
}