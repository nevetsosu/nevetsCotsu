using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using System.Reflection;
using Microsoft.VisualBasic;
using Microsoft.Extensions.DependencyInjection;

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

          Client.InteractionCreated += InteractionCreatedAsync;
          Handler.InteractionExecuted += InteractionExecutedAsync;
     }

     private async Task ReadyAsync() {
          await ServiceProvider.GetRequiredService<ILogger>().LogAsync("Handling Register Commands");
          await Handler.RegisterCommandsGloballyAsync();
     }

     private async Task InteractionCreatedAsync(SocketInteraction interaction) {
          try {
               SocketInteractionContext context = new SocketInteractionContext(Client, interaction);
               if (interaction.User.Id == AdwinModule.AdwinUserID) {
                    await interaction.RespondAsync("no not u");
                    return;
               }
               IResult result = await Handler.ExecuteCommandAsync(context, ServiceProvider);

               // Due to async nature of InteractionFramework, the result here may always be success.
               // That's why we also need to handle the InteractionExecuted event.
               if (!result.IsSuccess)
                    switch (result.Error)
                    {
                         case InteractionCommandError.UnmetPrecondition:
                              await ServiceProvider.GetRequiredService<ILogger>().LogAsync("[InteractionCreatedAsync] UnmetPrecondition");
                              // implement
                              break;
                         default:
                              break;
                    }

          } catch {
               if (interaction.Type is InteractionType.ApplicationCommand) {
                    await interaction.GetOriginalResponseAsync().ContinueWith(async (task) => await task.Result.DeleteAsync());
               }
          }
     }

     private async Task InteractionExecutedAsync(ICommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
            switch (result.Error)
            {
                case InteractionCommandError.UnmetPrecondition:
                    await ServiceProvider.GetRequiredService<ILogger>().LogAsync("[InteractionCreatedAsync] UnmetPrecondition");
                    break;
                default:
                    break;
            }
    }
}