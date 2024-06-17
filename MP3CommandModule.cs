using Discord;
using Discord.Interactions;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

public class MP3CommandModule : InteractionModuleBase<SocketInteractionContext> {
     private ILogger Logger;
     private ConcurrentDictionary<ulong, GuildData> GuildDataDict;

     public MP3CommandModule(ConcurrentDictionary<ulong, GuildData> guildDataDict, ILogger? logger = null) {
          GuildDataDict = guildDataDict;
          Logger = logger ?? new DefaultLogger();
     }

     [SlashCommand("play", "start the mp3 player")]
     public async Task Play(string? song = default) {
          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }

          // link validity check
          string? YoutubeID = null;
          if (song != null && string.IsNullOrEmpty(YoutubeID = GetYoutubeID(song))) { // normally u would do a lookup instead of saying an error
               await RespondAsync("Invalid song link...but heres a song anyway");
               YoutubeID = "dQw4w9WgXcQ";
          }
          else await RespondAsync("playing...");

          // check if it is a URL, other wise look it up on Youtube
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id

          switch (await guildData._MP3Handler.TryPlay(targetChannel, song == null ? null : @"https://www.youtube.com/v/" + YoutubeID)) {
               case MP3Handler.PlayerCommandStatus.EmptyQueue:
                    await ModifyOriginalResponseAsync((m) => m.Content = "queue is empty");
                    break;
               case MP3Handler.PlayerCommandStatus.Already:
                    if (!string.IsNullOrEmpty(song)) await ModifyOriginalResponseAsync((m) => m.Content = "song added to queue");
                    break;
               default:
                    break;
          }

     }

     private string? GetYoutubeID(string url) {
          const string pattern = @"^(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:watch\?(?:.*&)?v=|embed\/|v\/|shorts\/)|youtu\.be\/)(?<videoId>[A-Za-z0-9_-]{11})(?:[?&].*)?$";
          Match match = Regex.Match(url, pattern);
          if (match.Success) {
               return match.Groups["videoId"].Value;
          } else {
               Logger.LogAsync("Invalid URL");
               return null; // rick roll video ID on failure
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
               return; 
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

     [SlashCommand("resume", "resumes a previously loaded song")]
     public async Task ResumeSong() {
          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
          await RespondAsync("resuming...");

          switch (await guildData._MP3Handler.TryPlay(targetChannel)) {
               case MP3Handler.PlayerCommandStatus.EmptyQueue:
                    await ModifyOriginalResponseAsync((m) => m.Content = "no songs to resume");
                    break;
               case MP3Handler.PlayerCommandStatus.Already:
                    await ModifyOriginalResponseAsync((m) => m.Content = "already playing");
                    break;
               default:
                    break;
          }
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

     [SlashCommand("queue", "lists the current song queue")]
     public async Task Queue() {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger));
          List<MP3Handler.MP3Entry> entries = guildData._MP3Handler.GetQueueAsList();
          if (entries.Count == 0) {
               await RespondAsync("there are no songs in queue");
               return;
          }

          EmbedFieldBuilder[] Fields = new EmbedFieldBuilder[entries.Count];

          for (int i = 0; i < entries.Count; i++) {
               Fields[i] = new EmbedFieldBuilder().WithName("Song: ").WithValue(entries[i].URL);
          }
          Embed embed = new EmbedBuilder()
                         .WithTitle("Queue")
                         .WithFields(Fields)
                         .Build();
          await RespondAsync(embed: embed);
     }

     [SlashCommand("nowplaying", "shows details about the current song")]
     public async Task NowPlaying() {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
          MP3Handler.SongData? data = await guildData._MP3Handler.NowPlaying();

          if (data == null) {
               await RespondAsync("no song currently playing");
               return;
          }

          string VideoID = data.Value.URL.Substring(data.Value.URL.Length - 11, 11);
          await Logger.LogAsync($"[Debug/NowPlaying] Video ID: {VideoID}");

          long VideoProgressSeconds = await guildData._MP3Handler.NowPlayingProgress();
          string timestamp;
          if (VideoProgressSeconds / 3600 > 0) timestamp = $"{VideoProgressSeconds / 3600:00}{(VideoProgressSeconds / 60) % 60:00}:{VideoProgressSeconds % 60:00}";
          else timestamp = $"{(VideoProgressSeconds / 60) % 60:00}:{VideoProgressSeconds % 60:00}";

          Embed embed = new EmbedBuilder()
                         .WithTitle("Now playing")
                         .AddField(new EmbedFieldBuilder().WithName("URL").WithValue(data.Value.URL))
                         .WithThumbnailUrl($"https://img.youtube.com/vi/{VideoID}/default.jpg")
                         .AddField(new EmbedFieldBuilder().WithName("Progress").WithValue(timestamp))
                         .Build();
          await RespondAsync(embed: embed);
     }

     [SlashCommand("loop", "toggles looping")]
     public async Task Loop() {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
          await RespondAsync("toggling looping...");
          switch (await guildData._MP3Handler.ToggleLooping()) {
               case MP3Handler.PlayerCommandStatus.Ok2:
                    await ModifyOriginalResponseAsync(m => m.Content = "Looping");
                    break;
               case MP3Handler.PlayerCommandStatus.Ok:
                    await ModifyOriginalResponseAsync(m => m.Content = "no longer looping");
                    break;
               case MP3Handler.PlayerCommandStatus.Disconnected:
                    await ModifyOriginalResponseAsync(m => m.Content = "bot is currently disconnected");
                    break;
               case MP3Handler.PlayerCommandStatus.NotCurrentlyPlaying:
                    await ModifyOriginalResponseAsync(m => m.Content = "there is nothing to loop right now");
                    break;
               default:
                    break;
          };
     }

}