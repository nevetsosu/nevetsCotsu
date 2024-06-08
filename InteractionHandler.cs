using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using System.Reflection;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

public class GuildCommandData {
     public int PlayingLock; // 0 false 1 true
     public int CallCount;

     public GuildCommandData() {
          PlayingLock = 0;
          CallCount = 0;
     }
}

public class GuildData {
     public GuildCommandData LocosTacos;
     public GuildCommandData Ricky;
     public VoiceStateManager _VoiceStateManager;
     public MP3Handler _MP3Handler;
     public GuildData(ILogger logger) {
          LocosTacos = new GuildCommandData();
          Ricky = new GuildCommandData();
          _VoiceStateManager = new VoiceStateManager(logger);
          _MP3Handler = new MP3Handler(_VoiceStateManager, logger);
     }
}

public class InteractionHandler {
     private readonly DiscordSocketClient Client;
     private readonly InteractionService Handler;
     private readonly IServiceProvider ServiceProvider;
     private readonly ILogger Logger;
     private readonly ConcurrentDictionary<ulong, GuildData> GuildDataDict;

     public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider serviceprovider, ConcurrentDictionary<ulong, GuildData> guildDataDict, ILogger logger) {
          Client = client;
          Handler = handler;
          ServiceProvider = serviceprovider;
          Logger = logger;
          GuildDataDict = guildDataDict;
     }

     public async Task InitializeAsync() {
          Handler.Log += Logger.LogAsync;
          Client.Ready += ReadyAsync;
          Client.UserVoiceStateUpdated += VoiceChannelStatusUpdatedAsync;

          await Handler.AddModulesAsync(Assembly.GetEntryAssembly(), ServiceProvider);

          Client.InteractionCreated += InteractionCreatedAsync;
          // Handler.InteractionExecuted += InteractionExecutedAsync;
     }

     private async Task ReadyAsync() {
          await ServiceProvider.GetRequiredService<ILogger>().LogAsync("Handling Register Commands");
          await Handler.RegisterCommandsGloballyAsync();
     }

     private async Task InteractionCreatedAsync(SocketInteraction interaction) {
          try {
               SocketInteractionContext context = new SocketInteractionContext(Client, interaction);
               if (interaction.User.Id == AdwinModule.AdwinUserID && !AdwinModule.AllowAdwin) {
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

     private async Task VoiceChannelStatusUpdatedAsync(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState) {

          await Task.Delay(0);
     }

//      private async Task InteractionExecutedAsync(ICommandInfo commandInfo, IInteractionContext context, IResult result)
//     {
//         if (!result.IsSuccess)
//             switch (result.Error)
//             {
//                 case InteractionCommandError.UnmetPrecondition:
//                     await ServiceProvider.GetRequiredService<ILogger>().LogAsync("[InteractionCreatedAsync] UnmetPrecondition");
//                     break;
//                 default:
//                     break;
//             }
//     }
}