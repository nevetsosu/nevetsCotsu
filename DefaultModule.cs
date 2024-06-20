using Discord.WebSocket;
using Discord;
using Discord.Interactions;
using Serilog;

public class DefaultModule : InteractionModuleBase<SocketInteractionContext> {
     public static readonly ulong AdwinUserID = 390610273892827136UL;
     public static readonly ulong SamyUserID = 762049021514481685UL;
     public static readonly ulong nevetsBotsuID = 787116682266673184UL;
     public static readonly ulong AffectedUser = AdwinUserID;

     [SlashCommand("ping", "respond with pong and latency")]
     public async Task Ping() {
          Embed embed = new EmbedBuilder()
                    .WithTitle("Pong")
                    .AddField(new EmbedFieldBuilder().WithName("Gateway Latency").WithValue($"{Context.Client.Latency}ms"))
                    .Build();
          await RespondAsync(embed: embed);
     }

     [SlashCommand("mute", "mute a user")]
     public async Task MuteUser(SocketGuildUser user) {
          if (user.IsMuted) {
               await RespondAsync("user is already muted");
               return;
          }
          bool success = await TrySetMuteUser(user, true);
          await RespondAsync(success ? "done" : "failed to mute user");
     }

     [SlashCommand("unmute", "unmute a user")]
     public async Task UnMuteUser(SocketGuildUser user) {
          if (!user.IsMuted) {
               await RespondAsync("user is already unmuted");
               return;
          }

          bool success = await TrySetMuteUser(user, false);
          await RespondAsync(success ? "done" : "failed to unmute user");
     }

     private async Task<bool> TrySetMuteUser(IGuildUser user, bool mute) {
          try {
               await user.ModifyAsync(x => x.Mute = mute);
               return true;
          } catch {
               return false;
          }
     }

     [SlashCommand("eatmyassplz", "thirsty rojas")]
     public async Task EatMyAssPlz() {
          await RespondAsync("no");
     }
}