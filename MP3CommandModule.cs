using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Collections.Concurrent;

public class MP3CommandModule : InteractionModuleBase<SocketInteractionContext> {
     private ILogger Logger;
     private ConcurrentDictionary<ulong, GuildData> GuildDataDict;

     public MP3CommandModule(ConcurrentDictionary<ulong, GuildData> guildDataDict, ILogger logger) {
          GuildDataDict = guildDataDict;
          Logger = logger;
     }

     [SlashCommand("play", "start the mp3 player")]
     public async Task StartPlayer() {
          var Log = async (string str) => await Logger.LogAsync("[Debug/StartPlayer] " + str);

          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id

          if (guildData._MP3Handler.QueueCount == 0) {
               await RespondAsync("there are no songs queued");
               return;
          };

          await RespondAsync("Starting the Player...");
          if(!await guildData._MP3Handler.TryResume(targetChannel)) await ModifyOriginalResponseAsync((m) => m.Content = "Starting the Player...Failed to start");
     }

     [SlashCommand("queueadd", "add a song to the queue")]
     public async Task QueueAdd(string URL) {
          // do some kind of url validity check
          // checking if the link is valid should be handled in a seperate class

          await RespondAsync($"added {URL} to queue");
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
          // add to MP3Handler
          guildData._MP3Handler.AddQueue(new MP3Handler.MP3Entry(URL));
     }

     [SlashCommand("skip", "skip the current song")]
     public async Task SkipSong() {
          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }

          if (targetChannel != Context.Guild.CurrentUser.VoiceChannel) {
               await RespondAsync("you are not in the same channel");
          }

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
          await RespondAsync("skipping...");

          if (!await guildData._MP3Handler.SkipSong()) await ModifyOriginalResponseAsync((m) => m.Content = "skipping...failed");
     }

     [SlashCommand("resume", "resumes a previously loaded song")]
     public async Task ResumeSong() {
          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
          await RespondAsync("resuming...");

          if (!await guildData._MP3Handler.TryResume(targetChannel)) await ModifyOriginalResponseAsync((m) => m.Content = "resuming...failed");
     }

     [SlashCommand("pause", "pauses the current song")]
     public async Task PauseSong() {
          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }

          if (Context.Guild.CurrentUser.VoiceChannel == null) {
               await RespondAsync("bot is not in the channel");
          }

          if (targetChannel != Context.Guild.CurrentUser.VoiceChannel) {
               await RespondAsync("you are not in the same channel");
               return;
          }

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
          await RespondAsync("pausing...");
          await guildData._MP3Handler.Pause();
     }
}