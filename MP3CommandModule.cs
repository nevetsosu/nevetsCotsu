using Discord;
using Discord.Interactions;
using System.Collections.Concurrent;
using System.Text;
using YoutubeExplode.Videos;
using YoutubeExplode.Search;

public class MP3CommandModule : InteractionModuleBase<SocketInteractionContext> {
     private ILogger Logger;
     private ConcurrentDictionary<ulong, GuildData> GuildDataDict;
     private YTAPIManager ytAPIManager;

     public MP3CommandModule(ConcurrentDictionary<ulong, GuildData> guildDataDict, YTAPIManager ytAPIManager, ILogger? logger = null) {
          GuildDataDict = guildDataDict;
          Logger = logger ?? new DefaultLogger();
          this.ytAPIManager = ytAPIManager;
     }

     [SlashCommand("play", "Start the mp3 player or add song to queue.", runMode : RunMode.Async)]
     public async Task Play([Autocomplete(typeof(YTSearchAutocomplete))] string? song = null) {
          var Log = async (string str) => await Logger.LogAsync("[PlaySlashCommand] " + str);
          IVoiceChannel? targetChannel = (Context.User as IGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }

          await Log($"playing with song: {song ?? "null"}. EMPTY?: {string.IsNullOrEmpty(song)}");

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id

          if (string.IsNullOrEmpty(song)) { // no song
               await RespondAsync("trying player...");
               switch (await guildData._MP3Handler.TryPlay(targetChannel)) {
                    case MP3Handler.PlayerCommandStatus.EmptyQueue:
                         await ModifyOriginalResponseAsync((m) => m.Content = "queue is empty");
                         break;
                    case MP3Handler.PlayerCommandStatus.Already:
                         await ModifyOriginalResponseAsync((m) => m.Content = $"already playing");
                         break;
                    default:
                         break;
               }
               return;
          }

          string? YoutubeID;
          // link validity check or YT search
          if (!string.IsNullOrEmpty(YoutubeID = ytAPIManager.GetYoutubeID(song))) {
               await Log("youtube url identified, YTID: " + YoutubeID);
               await RespondAsync("adding to queue...");
          } else if (!string.IsNullOrEmpty(YoutubeID = await ytAPIManager.SearchForVideo(song))) {
               await Log("[Debug/Play] searched youtube successfully with YTID: " + YoutubeID);
               await RespondAsync("searching youtube...");
          } else {
               await Log("defaulted to rick roll");
               await RespondAsync("defaulting to rick roll");
               YoutubeID = "dQw4w9WgXcQ";
          }
          Video? VideoData = await ytAPIManager.GetVideoData(YoutubeID);

          // check if it is a URL, other wise look it up on Youtube
          switch (await guildData._MP3Handler.TryPlay(targetChannel, new MP3Handler.MP3Entry(YoutubeID, Context.User as IGuildUser, null, VideoData))) {
               case MP3Handler.PlayerCommandStatus.EmptyQueue:
                    await ModifyOriginalResponseAsync((m) => m.Content = "queue is empty");
                    break;
               case MP3Handler.PlayerCommandStatus.Already:
                    await ModifyOriginalResponseAsync((m) => m.Content = $"added to queue: [{VideoData?.Title}]({@"https://www.youtube.com/v/" + VideoData?.Id})");
                    break;
               case MP3Handler.PlayerCommandStatus.Ok:
                    await ModifyOriginalResponseAsync((m) => m.Content = $"playing [{VideoData?.Title}]({@"https://www.youtube.com/v/" + VideoData?.Id})");
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

     [SlashCommand("skip", "skip the current song", runMode : RunMode.Async)]
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

     [SlashCommand("resume", "resumes a previously loaded song", runMode : RunMode.Async)]
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
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
          MP3Handler.MP3Entry? data = await guildData._MP3Handler.NowPlaying();

          if (data == null) {
               await RespondAsync("no song currently playing");
               return;
          }
          await RespondAsync("thinking");

          long VideoProgressSeconds = await guildData._MP3Handler.NowPlayingProgress();
          string timestamp;
          if (VideoProgressSeconds / 3600 > 0) timestamp = $"{VideoProgressSeconds / 3600}{(VideoProgressSeconds / 60) % 60:00}:{VideoProgressSeconds % 60:00}";
          else timestamp = $"{(VideoProgressSeconds / 60) % 60:0}:{VideoProgressSeconds % 60:00}";

          if (data.VideoData?.Duration != null) {
               try {
                    timestamp += $"/{YTAPIManager.FormatTimeSpan(data.VideoData.Duration)}";
               } catch (Exception e) {
                    await Logger.LogAsync("failed to get normal timestamp: " + e.Message);
               }
          }

          StringBuilder strBuilder = new StringBuilder();

          List<MP3Handler.MP3Entry> QueueEntries = guildData._MP3Handler.GetQueueAsList();
          for (int i = 0; i < QueueEntries.Count; i++) {
               MP3Handler.MP3Entry entry = QueueEntries[i];
               if (entry.VideoData != null) strBuilder.AppendLine($"``{i + 1}.``[{entry.VideoData.Title}]({@"https://www.youtube.com/v/" + entry.VideoID})``{YTAPIManager.FormatTimeSpan(entry.VideoData.Duration)}``");
               else strBuilder.AppendLine($"``{i}.``Couldn't get song data");
          }

          EmbedBuilder builder = new EmbedBuilder()
                         .WithTitle("Now Playing")
                         .AddField(new EmbedFieldBuilder().WithName("Song").WithValue($"[{data.VideoData?.Title}]({@"https://www.youtube.com/v/" + data.VideoID})"))
                         .AddField(new EmbedFieldBuilder().WithName("Requested By").WithValue(data.RequestUser?.Mention ?? "``unknown``"))
                         .WithThumbnailUrl($"https://img.youtube.com/vi/{data.VideoID}/default.jpg")
                         .AddField(new EmbedFieldBuilder().WithName("Progress").WithValue($"``{timestamp}``"));

          if (QueueEntries.Count > 0)
               builder.AddField(new EmbedFieldBuilder().WithName("Queued Next").WithValue(strBuilder.ToString()));

          await ModifyOriginalResponseAsync(m => { m.Content = ""; m.Embed = builder.Build(); });
     }

     [SlashCommand("nowplaying", "shows details about the current song")]
     public async Task NowPlaying() {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData(Logger)); // error check this line, potential null deref with Context.Guild.Id
          MP3Handler.MP3Entry? data = await guildData._MP3Handler.NowPlaying();

          if (data == null) {
               await RespondAsync("no song currently playing");
               return;
          }
          await RespondAsync("thinking");

          long VideoProgressSeconds = await guildData._MP3Handler.NowPlayingProgress();
          string timestamp;
          if (VideoProgressSeconds / 3600 > 0) timestamp = $"{VideoProgressSeconds / 3600:00}:{(VideoProgressSeconds / 60) % 60:00}:{VideoProgressSeconds % 60:00}";
          else timestamp = $"{(VideoProgressSeconds / 60) % 60:0}:{VideoProgressSeconds % 60:00}";

          if (data.VideoData?.Duration != null) {
               try {
                    timestamp += $"/{YTAPIManager.FormatTimeSpan(data.VideoData.Duration)}";
               } catch (Exception e) {
                    await Logger.LogAsync("failed to get normal timestamp: " + e.Message);
               }
          }

          Embed embed = new EmbedBuilder()
                         .WithTitle("Now playing")
                         .AddField(new EmbedFieldBuilder().WithName("Song").WithValue($"[{data.VideoData?.Title}]({@"https://www.youtube.com/v/" + data.VideoID})"))
                         .AddField(new EmbedFieldBuilder().WithName("Requested By").WithValue(data.RequestUser?.Mention ?? "``unknown``"))
                         .WithThumbnailUrl($"https://img.youtube.com/vi/{data.VideoID}/default.jpg")
                         .AddField(new EmbedFieldBuilder().WithName("Progress").WithValue($"``{timestamp}``"))
                         .Build();
          await ModifyOriginalResponseAsync(m => { m.Content = ""; m.Embed = embed; });
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

     [SlashCommand("testmp3", "test command", runMode : RunMode.Async)]
     public async Task Test() {
          await RespondAsync("working");
          // Video? response = await ytAPIManager.GetVideoData("dQw4w9WgXcQ");
          await ytAPIManager.TestFunction();

          await ModifyOriginalResponseAsync(m => m.Content = "done");
     }

}

public class YTSearchAutocomplete : AutocompleteHandler {
     YTAPIManager ytAPIManager;
     ILogger Logger;
     public YTSearchAutocomplete(YTAPIManager ytAPIManager, ILogger? logger = null) {
          Logger = logger ?? new DefaultLogger();
          this.ytAPIManager = ytAPIManager;
     }
     public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo paraminfo, IServiceProvider serviceProvider) {
          const int MaxResults = 5;
          string? UserInput = interaction.Data.Current.Value.ToString();
          if (UserInput == null) return AutocompletionResult.FromSuccess();
          await Logger.LogAsync("autocompleting youtube based on: " + UserInput);

          IAsyncEnumerable<VideoSearchResult> ResultEnum = ytAPIManager.YTSearchResults(UserInput);

          List<AutocompleteResult> AutoCompleteResults = new(MaxResults);

          await ResultEnum.Take(MaxResults).ForEachAsync( (videoSearchResult) => {
               AutoCompleteResults.Add(new AutocompleteResult(videoSearchResult.Title, videoSearchResult.Url));
          });

          return AutocompletionResult.FromSuccess(AutoCompleteResults);
     }
}