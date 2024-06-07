using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Discord.Audio;
using Discord.Interactions;
using System.Diagnostics;
using FFMPEG;

public class AdwinModule : InteractionModuleBase<SocketInteractionContext> {
     public static readonly ulong AdwinUserID = 390610273892827136UL;
     ILogger Logger;
     ConcurrentDictionary<ulong, GuildData> GuildDataDict;

     public AdwinModule(ILogger logger, ConcurrentDictionary<ulong, GuildData> guildDataDict) {
          Logger = logger;
          GuildDataDict = guildDataDict;
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

     [SlashCommand("join", "tells the bot to join the channel", runMode: RunMode.Async)]
     private async Task JoinVoice() {
          IVoiceChannel? vChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (vChannel == null) {
               await RespondAsync("you are not currently in a voice channel!");
               return;
          }

          if (Context.Guild.CurrentUser.VoiceChannel == vChannel) {
               await RespondAsync("Bot is already in current channel");
               return;
          }

          IAudioClient? audioClient = await TryJoinVoiceChannel();
          
          if (audioClient == null) {
               await RespondAsync("failed join");
               return;
          }

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, (id) => new GuildData { AudioClient = audioClient });
          guildData.AudioClient = audioClient;

          await RespondAsync("joined");
     }

     private async Task<IAudioClient?> TryJoinVoiceChannel() {
          var Log = async (string str) => await Logger.LogAsync("[Debug/TryJoinVoiceChannel] " + str);
          if (Context.Guild?.CurrentUser == null) {
               await Log("failed initial check");
               return null;
          }

          IVoiceChannel? vChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (vChannel == null) {
               await Log("user is not currently in a voice channel!");
               return null;
          }


          IAudioClient? audioClient;
          try {
               audioClient = await vChannel.ConnectAsync(selfDeaf : true, selfMute : false);
          } catch (Exception e){
               await Log("Failed to connect to voice Channel " + e.ToString());
               return null;
          }

          if (audioClient == null) {
               await Log("ConnectAsync returned null?");
          }

          return audioClient;
     }


     [SlashCommand("leave", "leave current voice channel", runMode: RunMode.Async)]
     private async Task LeaveVoice() {

          if (Context.Guild?.CurrentUser?.VoiceChannel == null) {
               await RespondAsync("Bot is not connected to any voice Channel");
               return;
          }
          if (Context.Guild.CurrentUser.VoiceChannel != (Context.User as IGuildUser)?.VoiceChannel) {
               await RespondAsync("User is not in the same voice channel!");
               return;
          }

          bool success = await TryLeaveVoiceChannel();
          await RespondAsync(success ? "done!" : "failed to leave");
     }

     private async Task<bool> TryLeaveVoiceChannel() {
          var Log = async (string str) => await Logger.LogAsync("[Debug/TryLeaveVoiceChannel] " + str);

          if (Context.Guild?.CurrentUser == null) {
               await Log("failed initial check");
               return false;
          }

          if (Context.Guild.CurrentUser.VoiceChannel == null) {
               await Log("bot is already disconnected");
               return true;
          }

          try {
               await Context.Guild.CurrentUser.VoiceChannel.DisconnectAsync();
               return true;
          } catch (Exception e) {
               await Log("failed to leave: " + e.ToString());
               return false;
          }
     }

     [SlashCommand("locos", "play locos tacos", runMode: RunMode.Async)]
     private async Task TryPlaySound() {
          var Log = async (string str) => await Logger.LogAsync("[Debug/TryPlaySound] " + str);

          IAudioClient? audioClient = await TryJoinVoiceChannel();
          if (audioClient == null) {
               await RespondAsync("audio client null");
               return;
          }
          await RespondAsync("playing...");

          const string filepath = @"/home/nevets/code/dotnetDiscordBot/locostacos.mp3";
          if (!File.Exists(filepath))
          {
               await Log($"File '{filepath}' not found.");
               return;
          }

          using (var stream = audioClient.CreatePCMStream(AudioApplication.Music)) {
               await new FFMPEGHandler(Logger).ReadFileToStream(filepath, stream);
          }
          await Log("disconnecting");
          await audioClient.StopAsync();
          await ModifyOriginalResponseAsync((m) => m.Content = "done!");
     }

     private static Process? CreateStream(string path)
     {
          return Process.Start(new ProcessStartInfo
          {
               FileName = "ffmpeg",
               Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
               UseShellExecute = false,
               RedirectStandardOutput = true,
          });
     }
}