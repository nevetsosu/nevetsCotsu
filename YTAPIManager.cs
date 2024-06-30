using System.Text.RegularExpressions;

using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Search;
using Serilog;
using YoutubeExplode.Videos.Streams;
using Discord.Audio;

public class YTAPIManager {
     private readonly YoutubeClient YTClient;
     public YTAPIManager() {
          YTClient = new YoutubeClient();
     }

     public async Task<Video?> GetVideoData(string videoID) {
          return await YTClient.Videos.GetAsync(new VideoId(videoID));
     }

     public static string FormatTimeSpan(TimeSpan? time = null) {
          Log.Debug ("PTtoNormalTimeStamp processing TimeSpan: " + time.ToString());
          if (time == null) {
               Log.Debug("null time span, returning 00:00:00");
               return "00:00:00";
          }
          int hours = time.Value.Hours;
          int minutes = time.Value.Minutes;
          int seconds = time.Value.Seconds;

          // minutes padding is different depending on if hours is non-zero
          string timestamp = "";
          if (hours > 0) {
               timestamp += $"{hours}:";
               timestamp += $"{minutes:00}:";
          } else timestamp += $"{minutes:0}:";

          // seconds are always presented with the same formatting
          timestamp += $"{seconds:00}";

          return timestamp;
     }

     // this isnt really used since its so long
     public static string IDToURL(string VideoID) {
          return @"https://www.youtube.com/v/" + VideoID;
     }

     // returns a Video ID of a valid video or null
     public async Task<string?> SearchForVideo(string query) {
          IAsyncEnumerable<VideoSearchResult> ResultEnum = YTClient.Search.GetVideosAsync(query);
          VideoSearchResult result;
          try {
               result = await ResultEnum.FirstAsync();
          } catch (Exception e) {
               Log.Debug("[SearchForVideo] failed to find a single result for the search: " + e.Message);
               return null;
          }

          return result.Id;
     }

     public IAsyncEnumerable<VideoSearchResult> YTSearchResults(string query) {
          return YTClient.Search.GetVideosAsync(query);
     }

     public string? GetYoutubeID(string url) {
          const string pattern = @"^(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:watch\?(?:.*&)?v=|embed\/|v\/|shorts\/)|youtu\.be\/)(?<videoId>[A-Za-z0-9_-]{11})(?:[?&].*)?$";
          Match match = Regex.Match(url, pattern);
          if (match.Success) {
               return match.Groups["videoId"].Value;
          } else {
               Log.Debug("Invalid URL, returning null");
               return null; // rick roll video ID on failure
          }
     }

     public async Task TestFunction() {
          var client = new YoutubeClient();
          Video v = await client.Videos.GetAsync(new VideoId("dQw4w9WgXcQ"));
          Log.Debug("video title: " + v.Title);
     }

     public async Task<Stream> GetAudioStream(VideoId videoID) {
          var StreamManifest = await YTClient.Videos.Streams.GetManifestAsync(videoID);
          var AudioStreams = StreamManifest.GetAudioStreams();

          Log.Debug($"Found {AudioStreams.Count()} streams");

          var AudioStreamInfo = AudioStreams.GetWithHighestBitrate();

          Log.Debug($"Choose stream URL: {AudioStreamInfo.Url}");

          Stream stream = await YTClient.Videos.Streams.GetAsync(AudioStreamInfo);
          return stream;
     }

     public async Task TestReadAudioStream(VideoId VideoID) {
          var StreamManifest = await YTClient.Videos.Streams.GetManifestAsync(VideoID);
          var AudioStreams = StreamManifest.GetAudioStreams();

          Log.Debug($"Found {AudioStreams.Count()} streams");

          var AudioStreamInfo = AudioStreams.GetWithHighestBitrate();

          Log.Debug($"Choose stream URL: {AudioStreamInfo.Url}");

          byte[] buffer = new byte[1000];
          int red;
          using (Stream stream = await YTClient.Videos.Streams.GetAsync(AudioStreamInfo)) {
               red = await stream.ReadAsync(buffer, 0, 1000);
          }

          Log.Debug($"Red : {red} bytes");
     }
}