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
     public async Task StartPlayer(string? song = null) {
          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }
          await RespondAsync("playing...");
          // check if it is a URL, other wise look it up on Youtube
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id

          switch (await guildData._MP3Handler.TryPlay(targetChannel, song)) {
               case MP3Handler.PlayerCommandStatus.EmptyQueue:
                    await ModifyOriginalResponseAsync((m) => m.Content = "queue is empty");
                    break;
               default:
                    break;
          }

     }

     // [SlashCommand("queueadd", "add a song to the queue")]
     // public async Task QueueAdd(string URL) {
     //      // do some kind of url validity check
     //      // checking if the link is valid should be handled in a seperate class

     //      await RespondAsync($"added {URL} to queue");
     //      GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
     //      // add to MP3Handler
     //      guildData._MP3Handler.AddQueue(new MP3Handler.MP3Entry(URL));
     // }

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

          switch (await guildData._MP3Handler.SkipSong()) {
               case MP3Handler.PlayerCommandStatus.EmptyQueue:
                    await ModifyOriginalResponseAsync((m) => m.Content += "there are now no more songs in the queue");
                    break;
               case MP3Handler.PlayerCommandStatus.Disconnected:
                    await ModifyOriginalResponseAsync((m) => m.Content += "unexpected disconnect before next song");
                    break;
               default:
                    break;
          }
     }

     // [SlashCommand("resume", "resumes a previously loaded song")]
     // public async Task ResumeSong() {
     //      IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
     //      if (targetChannel == null) {
     //           await RespondAsync("you are not in a voice channel");
     //           return;
     //      }

     //      GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
     //      await RespondAsync("resuming...");

     //      switch (await guildData._MP3Handler.TryPlay(targetChannel)) {
     //           case MP3Handler.PlayerCommandStatus.EmptyQueue:
     //                await ModifyOriginalResponseAsync((m) => m.Content = "no songs to resume");
     //                break;
     //           case MP3Handler.PlayerCommandStatus.Already:
     //                await ModifyOriginalResponseAsync((m) => m.Content = "already playing");
     //                break;
     //           default:
     //                break;
     //      }
     // }

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

          switch (await guildData._MP3Handler.Pause()) {
               case MP3Handler.PlayerCommandStatus.Already:
                    await ModifyOriginalResponseAsync((m) => m.Content = "already paused");
                    break;
               case MP3Handler.PlayerCommandStatus.EmptyQueue: // substitute for: not currently playing
                    await ModifyOriginalResponseAsync((m) => m.Content = "not currently playing");
                    break;
               default:
                    break;
          }
     }
}