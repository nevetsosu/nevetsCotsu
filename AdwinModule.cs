using Discord;
using Discord.Audio;
using Discord.Interactions;

public class AdwinModule : InteractionModuleBase<SocketInteractionContext> {
     public static readonly ulong AdwinUserID = 390610273892827136UL;

     ILogger Logger;

     public AdwinModule(ILogger logger) {
          Logger = logger;
     }

     [SlashCommand("adwin", "toggle mute on adwin")]
     public async Task ToggleAdwinMute() {
          IGuildUser? user = await TryGetAdwin();

          // check if adwin is in this server
          if (user == null) {
               await RespondAsync("adwin is not in this server");
               return;
          }

          bool success = await TryToggleMute(user);
          if (!success) {
               await RespondAsync("failed to toggle. is he in a voice channel?");
               return;
          }

          await RespondAsync(success ? "done" : "failed to toggle. is he in a voice channel?");
     }

     private async Task<IGuildUser?> TryGetAdwin() {
          try {
               return Context.Guild.GetUser(AdwinUserID) ?? await (Context.Guild as IGuild).GetUserAsync(AdwinUserID);
          } catch {
               await Logger.LogAsync("[Debug/TryGetAdwin] Exception when trying to get Adwin!!");
               return null;
          }
     }

     private async Task<bool> TryToggleMute(IGuildUser user) {
          try {
               await user.ModifyAsync(x => x.Mute = !x.Mute.Value);
               return true;
          } catch {
               return false;
          }
     }

     [SlashCommand("join", "tells the bot to join the channel")]
     private async Task TryJoinVoiceChannel() {
          var Log = async (string str) => await Logger.LogAsync("[Debug/TryJoinVoiceChannel] " + str);

          if (Context.Guild == null) {
               await Log("Context.Guild null");
               await RespondAsync("failed to join");
               return;
          }

          IVoiceChannel? vChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (vChannel == null) {
               await Log("voiceChannel null");
               await RespondAsync("you are not current in a voice channel!");
               return;
          }

          if (Context.Guild.CurrentUser == null) {
               await Log("null Context.Guild.CurrentUser");
               await RespondAsync("failed to join");
               return;
          }

          if (Context.Guild.CurrentUser.VoiceChannel == vChannel) {
               await RespondAsync("Bot is already in current channel");
          }

          try {
               await vChannel.ConnectAsync();

          } catch (Exception e){
               await Log("Failed to connect to voice Channel " + e.ToString());
               await RespondAsync("failed to join");
               return;
          }

          await RespondAsync("joined!");
     }

     [SlashCommand("leave", "leave current voice channel")]
     private async Task TryLeaveVoiceChannel() {
          var Log = async (string str) => await Logger.LogAsync("[Debug/TryLeaveVoiceChannel] " + str);

          if (Context.Guild == null) {
               await Log("null Context.Guild");
               await RespondAsync("failed to leave");
               return;
          }

          if (Context.Guild.CurrentUser == null) {
               await Log("null Context.Guild.CurrentUser");
               await RespondAsync("failed to leave");
               return;
          }

          if (Context.Guild.CurrentUser.VoiceChannel == null) {
               await RespondAsync("Bot is not connected to any voice Channel");
               return;
          }

          if (Context.Guild.CurrentUser.VoiceChannel != (Context.User as IGuildUser)?.VoiceChannel) {
               await RespondAsync("User is not in the same voice channel!");
               return;
          }

          try {
               await Context.Guild.CurrentUser.VoiceChannel.DisconnectAsync();
          } catch (Exception e) {
               await RespondAsync("failed to leave " + e.ToString());
               return;
          }

          await RespondAsync("done!");
     }
}