using Discord;
using Discord.Interactions;

public class AdwinModule : InteractionModuleBase<SocketInteractionContext> {
     public static readonly ulong AdwinUserID = 390610273892827136UL;

     [SlashCommand("adwin", "toggle mute on adwin")]
     public async Task ToggleAdwinMute() {
          IGuildUser user = await (Context.Guild as IGuild).GetUserAsync(AdwinUserID);

          // check if adwin is in this server
          if (user == null) {
               await RespondAsync("adwin is not in this server");
               return;
          }

          // attempt toggle
          try {
               await user.ModifyAsync(x => x.Mute = !x.Mute.Value);
          } catch {
               await RespondAsync("failed to toggle. is he in a voice channel?");
               return;
          }

          await RespondAsync("done");
     }
}