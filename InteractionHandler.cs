using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using System.Reflection;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

using Serilog;
using Serilog.Events;

public class GuildCommandData {
     public int PlayingLock; // 0 false 1 true
     public int CallCount;

     public GuildCommandData() {
          PlayingLock = 0;
          CallCount = 0;
     }
}

public class GuildData {
     public VoiceStateManager _VoiceStateManager;
     public MP3Handler _MP3Handler;
     public GuildData() {
          _VoiceStateManager = new VoiceStateManager();
          _MP3Handler = new MP3Handler(_VoiceStateManager);
     }
}

public class InteractionHandler {
     private readonly DiscordSocketClient Client;
     private readonly InteractionService Handler;
     private readonly IServiceProvider ServiceProvider;
     private readonly ConcurrentDictionary<ulong, GuildData> GuildDataDict;

     public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider serviceprovider, ConcurrentDictionary<ulong, GuildData> guildDataDict) {
          Client = client;
          Handler = handler;
          ServiceProvider = serviceprovider;
          GuildDataDict = guildDataDict;
     }

     public async Task InitializeAsync() {
          Handler.Log += DiscordLogAsync;
          Client.Log += DiscordLogAsync;
          Client.Ready += ReadyAsync;
          Client.UserVoiceStateUpdated += VoiceChannelStatusUpdatedAsync;

          await Handler.AddModulesAsync(Assembly.GetEntryAssembly(), ServiceProvider);

          Client.InteractionCreated += InteractionCreatedAsync;
          // Handler.InteractionExecuted += InteractionExecutedAsync;
     }

     private static async Task DiscordLogAsync(LogMessage message)
{
     LogEventLevel severity = message.Severity switch
          {
               LogSeverity.Critical => LogEventLevel.Fatal,
               LogSeverity.Error => LogEventLevel.Error,
               LogSeverity.Warning => LogEventLevel.Warning,
               LogSeverity.Info => LogEventLevel.Information,
               LogSeverity.Verbose => LogEventLevel.Verbose,
               LogSeverity.Debug => LogEventLevel.Debug,
               _ => LogEventLevel.Information
          };
          Log.Write(severity, message.Exception, "[{Source}] " + message.Message, message.Source);
          await Task.CompletedTask;
     }

     private async Task ReadyAsync() {
          Log.Debug("Handling Register Commands");
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
                              Log.Error("[InteractionCreatedAsync] UnmetPrecondition");
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
//                     await Log.Error("[InteractionCreatedAsync] UnmetPrecondition");
//                     break;
//                 default:
//                     break;
//             }
//     }
}