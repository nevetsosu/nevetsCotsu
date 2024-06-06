using Discord.WebSocket;
using Discord;
using Discord.Interactions;

public class DefaultModule : InteractionModuleBase<SocketInteractionContext> {
     public static readonly ulong AdwinUserID = 390610273892827136UL;
     public static readonly ulong SamyUserID = 762049021514481685UL;
     public static readonly ulong nevetsBotsuID = 787116682266673184UL;
     public static readonly ulong AffectedUser = AdwinUserID;
     private readonly ILogger Logger;
     public DefaultModule(ILogger logger) {
          Logger = logger;
     }

     [SlashCommand("ping", "respond with pong")]
     public async Task Ping() {
          await RespondAsync("pong!");
     }

     [SlashCommand("mute", "mute a user")]
     public async Task MuteUser(SocketGuildUser user) {
          if (user.IsMuted) {
               await RespondAsync("user is already muted");
               return;
          }

          string response = "done";
          try {
               await user.ModifyAsync(x => x.Mute = true);
          } catch {
               response = "failed to mute user";
          } 
          
          await RespondAsync(response);
     }
     
     [SlashCommand("unmute", "unmute a user")]
     public async Task UnMuteUser(SocketGuildUser user) {
          if (!user.IsMuted) {
               await RespondAsync("user is already unmuted");
               return;
          }

          string response = "done";
          try {
               await user.ModifyAsync(x => x.Mute = false);
          } catch {
               response = "failed to unmute user";
          }
          await RespondAsync(response);
     }
}