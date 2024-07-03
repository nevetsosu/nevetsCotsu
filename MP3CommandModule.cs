using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using System.Collections.Concurrent;
using System.Text;
using YoutubeExplode.Videos;
using YoutubeExplode.Search;
using Serilog;

using MP3Logic;

public class MP3CommandModule : InteractionModuleBase<SocketInteractionContext> {
     private ConcurrentDictionary<ulong, GuildData> GuildDataDict;
     private YTAPIManager ytAPIManager;

     public MP3CommandModule(ConcurrentDictionary<ulong, GuildData> guildDataDict, YTAPIManager ytAPIManager) {
          GuildDataDict = guildDataDict;
          this.ytAPIManager = ytAPIManager;
     }

     [SlashCommand("play", "Start the mp3 player or add song to queue.", runMode : RunMode.Async)]
     public async Task Play([Autocomplete(typeof(YTSearchAutocomplete))] string? song = null) {
          SocketVoiceChannel? targetChannel = (Context.User as SocketGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData()); // error check this line, potential null deref with Context.Guild.Id

          if (string.IsNullOrEmpty(song)) { // no song
               await RespondAsync("trying player...");
               switch (await guildData._MP3Handler.TryPlay(targetChannel)) {
                    case PlayerCommandStatus.EmptyQueue:
                         await ModifyOriginalResponseAsync((m) => m.Content = "queue is empty");
                         break;
                    case PlayerCommandStatus.Already:
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
               Log.Debug("youtube url identified: " + song);
               await RespondAsync("adding to queue...");
          } else if (!string.IsNullOrEmpty(YoutubeID = await ytAPIManager.SearchForVideo(song))) {
               Log.Debug("[Debug/Play] searched youtube successfully ID: " + YoutubeID);
               await RespondAsync("searching youtube...");
          } else {
               Log.Debug("defaulted to rick roll");
               await RespondAsync("defaulting to rick roll");
               YoutubeID = "dQw4w9WgXcQ";
          }
          Video? VideoData = await ytAPIManager.GetVideoData(YoutubeID);
          if (VideoData == null) {
               await ModifyOriginalResponseAsync(m => m.Content = "couldn't get video data");
               return;
          }

          // check if it is a URL, other wise look it up on Youtube
          switch (await guildData._MP3Handler.TryPlay(targetChannel, new MP3Entry(VideoData, Context.User as SocketGuildUser, null))) {
               case PlayerCommandStatus.EmptyQueue:
                    await ModifyOriginalResponseAsync((m) => m.Content = "queue is empty");
                    break;
               case PlayerCommandStatus.Already:
                    await ModifyOriginalResponseAsync((m) => m.Content = $"added to queue: [{VideoData?.Title}]({@"https://www.youtube.com/v/" + VideoData?.Id})");
                    break;
               case PlayerCommandStatus.Ok:
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
     //      guildData._MP3Handler.AddQueue(new MP3Entry(URL));
     // }

     [SlashCommand("skip", "skip the current song", runMode : RunMode.Async)]
     public async Task SkipSong() {
          SocketVoiceChannel? targetChannel = (Context.User as SocketGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }

          if (targetChannel != Context.Guild.CurrentUser.VoiceChannel) {
               await RespondAsync("you are not in the same channel");
               return;
          }

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData()); // error check this line, potential null deref with Context.Guild.Id
          await RespondAsync("skipping...");

          switch (await guildData._MP3Handler.SkipSong()) {
               case PlayerCommandStatus.EmptyQueue:
                    await ModifyOriginalResponseAsync((m) => m.Content += "there are now no more songs in the queue");
                    break;
               case PlayerCommandStatus.Disconnected:
                    await ModifyOriginalResponseAsync((m) => m.Content += "unexpected disconnect before next song");
                    break;
               default:
                    break;
          }
     }

     [SlashCommand("resume", "resumes a previously loaded song", runMode : RunMode.Async)]
     public async Task ResumeSong() {
          SocketVoiceChannel? targetChannel = (Context.User as SocketGuildUser)?.VoiceChannel;
          if (targetChannel == null) {
               await RespondAsync("you are not in a voice channel");
               return;
          }

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData()); // error check this line, potential null deref with Context.Guild.Id
          await RespondAsync("resuming...");

          switch (await guildData._MP3Handler.TryPlay(targetChannel)) {
               case PlayerCommandStatus.EmptyQueue:
                    await ModifyOriginalResponseAsync((m) => m.Content = "no songs to resume");
                    break;
               case PlayerCommandStatus.Already:
                    await ModifyOriginalResponseAsync((m) => m.Content = "already playing");
                    break;
               default:
                    break;
          }
     }

     [SlashCommand("pause", "pauses the current song")]
     public async Task PauseSong() {
          SocketVoiceChannel? targetChannel = (Context.User as SocketGuildUser)?.VoiceChannel;
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

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData()); // error check this line, potential null deref with Context.Guild.Id
          await RespondAsync("pausing...");

          switch (await guildData._MP3Handler.Pause()) {
               case PlayerCommandStatus.Already:
                    await ModifyOriginalResponseAsync((m) => m.Content = "already paused");
                    break;
               case PlayerCommandStatus.EmptyQueue: // substitute for: not currently playing
                    await ModifyOriginalResponseAsync((m) => m.Content = "not currently playing");
                    break;
               default:
                    break;
          }
     }

     [SlashCommand("queue", "lists the current song queue")]
     public async Task Queue() {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData()); // error check this line, potential null deref with Context.Guild.Id
          MP3Entry? data = await guildData._MP3Handler.NowPlaying();

          if (data == null) {
               await RespondAsync("no song currently playing");
               return;
          }

          await RespondAsync("thinking");

          long VideoProgressSeconds = await guildData._MP3Handler.NowPlayingProgress();
          string timestamp;
          if (VideoProgressSeconds / 3600 > 0) timestamp = $"{VideoProgressSeconds / 3600}:{(VideoProgressSeconds / 60) % 60:00}:{VideoProgressSeconds % 60:00}";
          else timestamp = $"{(VideoProgressSeconds / 60) % 60:0}:{VideoProgressSeconds % 60:00}";

          if (data.VideoData?.Duration != null) {
               try {
                    timestamp += $"/{YTAPIManager.FormatTimeSpan(data.VideoData.Duration)}";
               } catch (Exception e) {
                    Log.Debug("failed to get normal timestamp: " + e.Message);
               }
          }

          string AdditionalStatus;
          if (guildData._MP3Handler.Looping) {
               AdditionalStatus = " ``Looping``";
          } else AdditionalStatus = string.Empty;

          StringBuilder strBuilder = new StringBuilder();

          List<MP3Entry> QueueEntries = guildData._MP3Handler.GetQueueAsList();
          Log.Debug("GetQueueAsList result: " + QueueEntries.Count);
          for (int i = 0; i < QueueEntries.Count; i++) {
               MP3Entry entry = QueueEntries[i];
               if (entry.VideoData != null) strBuilder.AppendLine($"\u202A``{i + 1}.``[{entry.VideoData.Title}]({@"https://www.youtube.com/v/" + entry.VideoData.Id})\u202C``{YTAPIManager.FormatTimeSpan(entry.VideoData.Duration)}``");
               else strBuilder.AppendLine($"``{i}.``Couldn't get song data");
          }

          EmbedBuilder builder = new EmbedBuilder()
                         .WithTitle("Now Playing")
                         .AddField(new EmbedFieldBuilder().WithName("Song").WithValue($"[{data.VideoData?.Title}]({@"https://www.youtube.com/v/" + data.VideoData?.Id})"))
                         .AddField(new EmbedFieldBuilder().WithName("Requested By").WithValue(data.RequestUser?.Mention ?? "``unknown``"))
                         .WithThumbnailUrl($"https://img.youtube.com/vi/{data.VideoData?.Id}/default.jpg")
                         .AddField(new EmbedFieldBuilder().WithName("Progress").WithValue($"``{timestamp}``{AdditionalStatus}"));

          if (QueueEntries.Count > 0)
               builder.AddField(new EmbedFieldBuilder().WithName("Queued Next").WithValue(strBuilder.ToString()));

          await ModifyOriginalResponseAsync(m => { m.Content = ""; m.Embed = builder.Build(); });
     }

     [SlashCommand("nowplaying", "shows details about the current song")]
     public async Task NowPlaying() {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData()); // error check this line, potential null deref with Context.Guild.Id
          MP3Entry? data = await guildData._MP3Handler.NowPlaying();

          if (data == null) {
               await RespondAsync("no song currently playing");
               return;
          }
          await RespondAsync("thinking");

          long VideoProgressSeconds = await guildData._MP3Handler.NowPlayingProgress();
          string timestamp;
          if (VideoProgressSeconds / 3600 > 0) timestamp = $"{VideoProgressSeconds / 3600:00}:{(VideoProgressSeconds / 60) % 60:00}:{VideoProgressSeconds % 60:00}";
          else timestamp = $"{(VideoProgressSeconds / 60) % 60:0}:{VideoProgressSeconds % 60:00}";

          if (data.VideoData.Duration != null) {
               try {
                    timestamp += $"/{YTAPIManager.FormatTimeSpan(data.VideoData.Duration)}";
               } catch (Exception e) {
                    Log.Debug("failed to get normal timestamp: " + e.Message);
               }
          }

          string AdditionalStatus;
          if (guildData._MP3Handler.Looping) {
               Log.Debug("adding looping status");
               AdditionalStatus = " ``Looping``";
          } else AdditionalStatus = string.Empty;

          Embed embed = new EmbedBuilder()
                         .WithTitle("Now playing")
                         .AddField(new EmbedFieldBuilder().WithName("Song").WithValue($"[{data.VideoData.Title}]({@"https://www.youtube.com/v/" + data.VideoData.Id})"))
                         .AddField(new EmbedFieldBuilder().WithName("Requested By").WithValue(data.RequestUser?.Mention ?? "``unknown``"))
                         .WithThumbnailUrl($"https://img.youtube.com/vi/{data.VideoData.Id}/default.jpg")
                         .AddField(new EmbedFieldBuilder().WithName("Progress").WithValue($"``{timestamp}``{AdditionalStatus}"))
                         .Build();
          await ModifyOriginalResponseAsync(m => { m.Content = ""; m.Embed = embed; });
     }

     [SlashCommand("loop", "toggles looping")]
     public async Task Loop() {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData()); // error check this line, potential null deref with Context.Guild.Id
          await RespondAsync("toggling looping...");
          switch (await guildData._MP3Handler.ToggleLooping()) {
               case PlayerCommandStatus.Ok2:
                    await ModifyOriginalResponseAsync(m => m.Content = "Looping");
                    break;
               case PlayerCommandStatus.Ok:
                    await ModifyOriginalResponseAsync(m => m.Content = "no longer looping");
                    break;
               case PlayerCommandStatus.Disconnected:
                    await ModifyOriginalResponseAsync(m => m.Content = "bot is currently disconnected");
                    break;
               case PlayerCommandStatus.NotCurrentlyPlaying:
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

     [SlashCommand("volume", "set the volume")]
     public async Task Volume([Summary(description: "A number between 0 and 100"), MinValue(0), MaxValue(100)] int? volume = null) {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData());

          float Volume = guildData._MP3Handler.Volume;

          if (volume == null) {
               await RespondAsync($"Current Volume: {Volume * 100:0.}%");
               return;
          }

          if (volume.Value > 100 || volume.Value < 0) {
               Log.Error($"volume cannot be out of range of [0, 100]: {volume.Value}");
               volume = int.Clamp(volume.Value, 0, 100);
          }
          guildData._MP3Handler.Volume = (float)volume.Value / 100;
          await RespondAsync($"Changed volume from: {Volume * 100:0.}% to {guildData._MP3Handler.Volume * 100:0.}%");
     }

     [SlashCommand("remove", "remove song from queue")]
     public async Task Remove([MinValue(1)]int index) {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData());
          try {
               guildData._MP3Handler.Remove(index - 1);
               await RespondAsync("removed song");
          } catch (InvalidOperationException) {
               Log.Debug($"failed to remove index {index}: invalid operation (list is empty)");
               await RespondAsync("no songs queued to remove");
          } catch (ArgumentOutOfRangeException) {
               Log.Debug($"failed to remove index {index}: ArgumentOutOfRangeException (out of range)");
               await RespondAsync("index out of range");
          } catch (Exception e) { 
               Log.Error($"failed to remove index {index}: UNHANDLED IN REMOVE FUNCTION: " + e.Message);
          }
     }

     [SlashCommand("move", "swap the position of two songs in the queue")]
     public async Task Move([MinValue(1)] int IndexA, [MinValue(1)] int IndexB) {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData());
          if (IndexA == IndexB) {
               await RespondAsync("both are the same song!");
               return;
          }

          try {
               guildData._MP3Handler.Swap(IndexA - 1, IndexB - 1);
               await RespondAsync("swapped!");
          } catch (Exception e) {
               Log.Debug(e.ToString());
               await RespondAsync("failed to swap. perhaps the indexes were out of range?");
          }
     }

     [SlashCommand("clear", "clears the queue")]
     public async Task Clear() {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData());
          guildData._MP3Handler.ClearQueue();

          await RespondAsync("cleared queue");
     }

     [SlashCommand("skipto", "skip to the song in the queue, discarding all the songs before it")]
     public async Task SkipTo([MinValue(1)] int index) {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData());
          try {
               guildData._MP3Handler.SkipTo(index - 1);
          } catch (ArgumentOutOfRangeException) {
               Log.Debug("index was out of range????");
               await RespondAsync("failed to skip to");
               return;
          }

          await RespondAsync("skipped to");
     }

     [SlashCommand("info", "get info a queue entry")]
     public async Task Info([MinValue(1)] int index) {
          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData());
          MP3Entry? entry;
          try {
               entry = guildData._MP3Handler.GetEntry(index - 1);
          } catch (ArgumentOutOfRangeException) {
               Log.Debug("index is out of range??");
               await RespondAsync("invalid index");
               return;
          }

          if (entry == null) {
               await RespondAsync("Queue is empty");
               return;
          }

          Embed embed = new EmbedBuilder()
                         .WithTitle("Entry Info")
                         .AddField(new EmbedFieldBuilder().WithName("Song").WithValue($"[{entry.VideoData.Title}]({@"https://www.youtube.com/v/" + entry.VideoData.Id})"))
                         .AddField(new EmbedFieldBuilder().WithName("Requested By").WithValue(entry.RequestUser?.Mention ?? "``unknown``"))
                         .WithThumbnailUrl($"https://img.youtube.com/vi/{entry.VideoData.Id}/default.jpg")
                         .Build();

          await RespondAsync(embed: embed);
     }

     [SlashCommand("seek", "seek through the current song. (but not past the last 30 seconds of the song)", runMode : RunMode.Async)]
     public async Task Seek([Summary("time", "A specific time in HH:mm:ss format")] string time) {
          TimeSpan start;
          if (!TimeSpan.TryParse(time, out start)) {
               await RespondAsync("time needs to be in HH:mm:ss format");
               return;
          }

          await RespondAsync("Seeking to: " + start);

          Log.Debug("Using time stamp: " + start);

          GuildData guildData = GuildDataDict.GetOrAdd(Context.Guild.Id, new GuildData());
          switch (await guildData._MP3Handler.Seek(start)) {
               case PlayerCommandStatus.NotCurrentlyPlaying:
                    await ModifyOriginalResponseAsync(m => m.Content = "nothing is currently playing");
                    break;
               case PlayerCommandStatus.InvalidArgument:
                    await ModifyOriginalResponseAsync(m => m.Content = "Duration unknown, cannot seek right now");
                    break;
               case PlayerCommandStatus.OutOfRange:
                    await ModifyOriginalResponseAsync(m => m.Content = "cannot seek that far ahead");
                    break;
               default:
                    break;
          };
     }

}

public class YTSearchAutocomplete : AutocompleteHandler {
     YTAPIManager ytAPIManager;
     public YTSearchAutocomplete(YTAPIManager ytAPIManager) {
          this.ytAPIManager = ytAPIManager;
     }
     public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo paraminfo, IServiceProvider serviceProvider) {
          const int MaxResults = 5;
          string? UserInput = interaction.Data.Current.Value.ToString();
          if (string.IsNullOrEmpty(UserInput)) return AutocompletionResult.FromSuccess();

          IAsyncEnumerable<VideoSearchResult> ResultEnum = ytAPIManager.YTSearchResults(UserInput);

          List<AutocompleteResult> AutoCompleteResults = new(MaxResults);

          await ResultEnum.Take(MaxResults).ForEachAsync( (videoSearchResult) => {  // 100 is the max field width for discord autocomplete fields
               string Title = videoSearchResult.Title;
               AutoCompleteResults.Add(new AutocompleteResult(Title.Length > 100 ? Title.Substring(0, 100) : Title, videoSearchResult.Url));
          });

          return AutocompletionResult.FromSuccess(AutoCompleteResults);
     }
}