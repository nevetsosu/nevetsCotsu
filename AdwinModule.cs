using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.Interactions;

public class AdwinModule : InteractionModuleBase<SocketInteractionContext> {
     public static bool AllowAdwin = true;
     public static readonly ulong AdwinUserID = 390610273892827136UL;
     ILogger Logger;
     ConcurrentDictionary<ulong, GuildData> GuildDataDict;

     public AdwinModule(ConcurrentDictionary<ulong, GuildData> guildDataDict, ILogger? logger = null) {
          Logger = logger ?? new DefaultLogger();
          GuildDataDict = guildDataDict;
     }

     [SlashCommand("toggleallowadwin", "toggles whether adwin is allowed to send commands or not")]
     public async Task ToggleAllowAdwin() {
          AllowAdwin = !AllowAdwin;
          await RespondAsync(AllowAdwin ? "adwin is now allowed" : "adwin is no longer allowed");
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
               await user.ModifyAsync(x => x.Mute = !user.IsMuted);
               return true;
          } catch {
               return false;
          }
     }

     [SlashCommand("join", "tells the bot to join the channel", runMode: RunMode.Async)]
     private async Task JoinVoice() {
          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not currently in a voice channel!");
               return;
          }

          if (Context.Guild.CurrentUser.VoiceChannel == targetChannel) {
               await RespondAsync("Bot is already in current channel");
               return;
          }

          await RespondAsync("Joining Voice...");

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger));

          IAudioClient? audioClient = await guildData._VoiceStateManager.ConnectAsync(targetChannel);
          if (audioClient == null) {
               await ModifyOriginalResponseAsync((m) => m.Content = "Joining Voice...Failed");
               return;
          }
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

          await RespondAsync("Leaving...");

          bool success = await TryLeaveVoiceChannel();
          if (!success) await ModifyOriginalResponseAsync((m) => m.Content = "Leaving...Failed");
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
               await Log("failed to leave: " + e.Message);
               return false;
          }
     }


     // spammable but still cancellable
     [SlashCommand("locos", "play locos tacos", runMode: RunMode.Async)]
     private async Task PlayLocosTacos([Summary("leave", "determines whether the bot leaves when it finishes")] bool leave = true) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/PlayLocosTacos] " + str);

          // Check if file exists
          const string filepath = @"/home/nevets/code/dotnetDiscordBot/locostacos.mp3";
          if (!File.Exists(filepath))
          {
               await RespondAsync($"Audio not found.");
               await Log($"File '{filepath}' not found!");
               return;
          }

          // Check if user is in a channel
          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel!");
               return;
          }
          await RespondAsync("Playing...");
          
          // Get Guild Data
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger));
          // GuildCommandData LocosTacos = guildData.LocosTacos;
          // await Log("Locos Count on this call " + LocosTacos.CallCount.ToString());
          await guildData._MP3Handler.LocosTacosInterrupt(targetChannel);
          // // Decide to play or queue
          // Interlocked.Increment(ref LocosTacos.CallCount);
          // if (Interlocked.CompareExchange(ref LocosTacos.PlayingLock, 1, 0) != 0) {
          //      await RespondAsync("added to queue");
          //      return;
          // }
          // else await RespondAsync("playing...");
          // //
          // // Playing Logic Critical Section
          // //

          // // Join Voice Channel

          // IAudioClient? audioClient = await guildData._VoiceStateManager.ConnectAsync(targetChannel);
          // if (audioClient == null || audioClient.ConnectionState != ConnectionState.Connected) {
          //      await ModifyOriginalResponseAsync((m) => m.Content = "Playing...Failed");
          //      Interlocked.And(ref LocosTacos.PlayingLock, 0);
          //      return;
          // }

          // // Play as many times as their have been commands on this Guild
          // try {
          //      FFMPEGHandler ffmpeg = new FFMPEGHandler(Logger);
          //      using (var stream = audioClient.CreatePCMStream(AudioApplication.Music)) {
          //           do {
          //                // Execute as many as there were calls
          //                do {
          //                     await ffmpeg.ReadFileToStream(filepath, stream, CancellationToken.None, 1.0f);
          //                } while (Interlocked.Decrement(ref LocosTacos.CallCount) > 0);

          //                await Task.Delay(1000); // wait 1 seconds before disconnect to see if there are more requests
          //           } while (Interlocked.CompareExchange(ref LocosTacos.CallCount, 0, 0) > 0);
          //      }
          //      if (leave) await audioClient.StopAsync();
          // } catch (System.Net.WebSockets.WebSocketException) {
          //      await Log("sudden disconnect handled");
          // }

          // Interlocked.And(ref LocosTacos.PlayingLock, 0);
     }

     private async Task TryPlaySound(bool leave) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/TryPlaySound] " + str);

          const string filepath = @"/home/nevets/code/dotnetDiscordBot/locostacos.mp3";
          if (!File.Exists(filepath))
          {
               await RespondAsync($"File '{filepath}' not found.");
               await Log($"File '{filepath}' not found.");
               return;
          }

          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not currently in a voice channel!");
               return;
          }
          await RespondAsync("Playing...");

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger));
          IAudioClient? audioClient = await  guildData._VoiceStateManager.ConnectAsync(targetChannel);
          if (audioClient == null || audioClient.ConnectionState != ConnectionState.Connected) {
               await ModifyOriginalResponseAsync((m) => m.Content = "Playing...Failed");
               return;
          }

          try {
               using (var stream = audioClient.CreatePCMStream(AudioApplication.Music)) {
                    await new FFMPEGHandler(Logger).ReadFileToStream(filepath, stream, CancellationToken.None);
               }
          } catch {
               await ModifyOriginalResponseAsync((m) => m.Content = "Playing...Interrupted");
               return;
          }

          if (audioClient.ConnectionState == ConnectionState.Connected && leave) await audioClient.StopAsync();
     }

     // spammable but still cancellable
     [SlashCommand("ricky", "ricky", runMode: RunMode.Async)]
     private async Task Ricky([Summary("leave", "determines whether the bot leaves when it finishes")] bool leave = true) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/PlayLocosTacos] " + str);

          // Check if file exists
          const string filepath = @"/home/nevets/code/dotnetDiscordBot/locostacos.mp3";
          if (!File.Exists(filepath))
          {
               await RespondAsync($"Audio not found.");
               await Log($"File '{filepath}' not found!");
               return;
          }

          // Check if user is in a channel
          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel!");
               return;
          }

          // Get Guild Data
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, (id) => new GuildData(Logger));
          GuildCommandData Ricky = guildData.Ricky;
          await Log("Locos Count on this call " + Ricky.CallCount.ToString());

          // Decide to play or queue
          Interlocked.Increment(ref Ricky.CallCount);
          if (Interlocked.CompareExchange(ref Ricky.PlayingLock, 1, 0) != 0) {
               await RespondAsync("added to queue");
               return;
          }
          else await RespondAsync("playing...");
          //
          // Playing Logic Critical Section
          //

          // Join Voice Channel
          IAudioClient? audioClient = await guildData._VoiceStateManager.ConnectAsync(targetChannel);
          if (audioClient == null || audioClient.ConnectionState != ConnectionState.Connected) {
               await ModifyOriginalResponseAsync((m) => m.Content = "Playing...Failed");
               Interlocked.And(ref Ricky.PlayingLock, 0);
               return;
          }

          // Play as many times as their have been commands on this Guild
          try {
               FFMPEGHandler ffmpeg = new FFMPEGHandler(Logger);
               using (var stream = audioClient.CreatePCMStream(AudioApplication.Music)) {
                    do {
                         // Execute as many as there were calls
                         do {
                              await ffmpeg.YoutubeToStream("https://www.youtube.com/watch?v=dQw4w9WgXcQ", stream, CancellationToken.None, 1.0f);
                         } while (Interlocked.Decrement(ref Ricky.CallCount) > 0);

                         await Task.Delay(1000); // wait 1 seconds before disconnect to see if there are more requests
                    } while (Interlocked.CompareExchange(ref Ricky.CallCount, 0, 0) > 0);
               }
               if (leave) await audioClient.StopAsync();
          } catch (System.Net.WebSockets.WebSocketException) {
               await Log("sudden disconnect handled");
          }

          Interlocked.And(ref Ricky.PlayingLock, 0);
     }
}