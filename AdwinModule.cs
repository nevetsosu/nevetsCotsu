using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.Interactions;
using Discord.WebSocket;

using Serilog;
public class AdwinModule : InteractionModuleBase<SocketInteractionContext> {
     public static bool AllowAdwin = true;
     public static readonly ulong AdwinUserID = 390610273892827136UL;
     readonly ConcurrentDictionary<ulong, GuildData> GuildDataDict;

     public AdwinModule(ConcurrentDictionary<ulong, GuildData> guildDataDict) {

          GuildDataDict = guildDataDict;
     }

     [SlashCommand("toggleallowadwin", "toggles whether adwin is allowed to send commands or not")]
     public async Task ToggleAllowAdwin() {
          AllowAdwin = !AllowAdwin;
          await RespondAsync(AllowAdwin ? "adwin is now allowed" : "adwin is no longer allowed");
     }

     [SlashCommand("adwin", "toggle mute on adwin")]
     public async Task ToggleAdwinMute() {
          SocketGuildUser? user = await TryGetAdwin();

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

     private async Task<SocketGuildUser?> TryGetAdwin() {
          SocketGuildUser? Adwin = Context.Guild.GetUser(AdwinUserID); // try to get from user cache
          if (Adwin == null) { // get info directly using rest call
               IUser restAdwin = await (Context.Guild as IGuild).GetUserAsync(AdwinUserID);
               Adwin = restAdwin as SocketGuildUser;
          }

          return Adwin;
     }

     private async Task<bool> TryToggleMute(IGuildUser user) {
          try {
               await user.ModifyAsync(x => x.Mute = !user.IsMuted);
               return true;
          } catch {
               return false;
          }
     }

     [SlashCommand("join", "tells the bot to join the channel", runMode: RunMode.Async)]
     public async Task JoinVoice() {
          SocketVoiceChannel? targetChannel = (Context.User as SocketGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not currently in a voice channel!");
               return;
          }

          if (Context.Guild.CurrentUser.VoiceChannel == targetChannel) {
               await RespondAsync("Bot is already in current channel");
               return;
          }

          await RespondAsync("Joining Voice...");

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData());

          IAudioClient? audioClient = await guildData._VoiceStateManager.ConnectAsync(targetChannel);
          if (audioClient == null) {
               await ModifyOriginalResponseAsync((m) => m.Content = "Joining Voice...Failed");
               return;
          }
     }


     [SlashCommand("leave", "leave current voice channel", runMode: RunMode.Async)]
     public async Task LeaveVoice() {
          if (Context.Guild?.CurrentUser?.VoiceChannel == null) {
               await RespondAsync("Bot is not connected to any voice Channel");
               return;
          }
          if (Context.Guild.CurrentUser.VoiceChannel != (Context.User as SocketGuildUser)?.VoiceChannel) {
               await RespondAsync("User is not in the same voice channel!");
               return;
          }

          await RespondAsync("Leaving...");

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData());
          await guildData._VoiceStateManager.DisconnectAsync();
     }

     // private async Task<bool> TryLeaveVoiceChannel() {
     //      var Log = async (string str) => await Logger.LogAsync("[Debug/TryLeaveVoiceChannel] " + str);

     //      if (Context.Guild?.CurrentUser == null) {
     //           await Log("failed initial check");
     //           return false;
     //      } 

     //      if (Context.Guild.CurrentUser.VoiceChannel == null) {
     //           await Log("bot is already disconnected");
     //           return true;
     //      }

     //      try {
     //           await Context.Guild.CurrentUser.VoiceChannel.DisconnectAsync();
     //           return true;
     //      } catch (Exception e) {
     //           await Log("failed to leave: " + e.Message);
     //           return false;
     //      }
     // }
}