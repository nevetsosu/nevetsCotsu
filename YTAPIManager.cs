using System.Text.RegularExpressions;

using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Search;
using Serilog;
using Discord.Audio;

public class YTAPIManager {
     private static readonly YoutubeClient YTClient = new YoutubeClient();

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

     // gets live or regular video id based on video meta data
     public static async Task<string> GetMediaURL(Video videoMetaData) {
          var videoID = videoMetaData.Id;

          if (videoMetaData.Duration == null) {
               try {
                    return await YTClient.Videos.Streams.GetHttpLiveStreamUrlAsync(videoID);
               } catch (Exception e) {
                    Log.Warning($"Tried to return a live stream link for ID: {videoID} but failed\ntrying a video stream url: {e.ToString}");
               }
          }

          var StreamManifest = await YTClient.Videos.Streams.GetManifestAsync(videoID);
          var AudioStreams = StreamManifest.GetAudioStreams();
          Log.Debug($"Found {AudioStreams.Count()} streams");

          var AudioStreamInfo = AudioStreams.GetWithHighestBitrate();

          Log.Debug($"Choose stream URL: {AudioStreamInfo.Url}");

          return AudioStreamInfo.Url;
     }
}