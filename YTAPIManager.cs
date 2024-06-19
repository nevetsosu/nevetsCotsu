using Google.Apis.Services;
using Google.Apis.YouTube.v3.Data;
using Google.Apis.YouTube.v3;
using System.Text.RegularExpressions;
using System.Collections.Immutable;
public class YTAPIManager {
     private YouTubeService YTService;
     private ILogger Logger;
     public YTAPIManager(string apiKey, string applicationName, ILogger? logger = null) {
          Logger = logger ?? new DefaultLogger();
          YTService = new YouTubeService(new BaseClientService.Initializer() {
               ApiKey = apiKey,
               ApplicationName = applicationName,
          });
     }

     public async Task<Video?> GetVideoData(string videoID) {
          VideosResource.ListRequest? VideoRequest = YTService?.Videos.List("snippet, contentDetails");
          if (VideoRequest == null) return null;
          VideoRequest.Id = videoID;

          VideoListResponse VideoResponse = await VideoRequest.ExecuteAsync();
          foreach (Video v in VideoResponse.Items) {
               await Logger.LogAsync($"Video Name: {v.Snippet.Title}\nVideo Duration: {v.ContentDetails.Duration}");
          }
          if (VideoResponse.Items.Count == 0) return null;
          return VideoResponse.Items.First();
     }

     public static string PTtoNormalTimeStamp(string PTTime) {
          new DefaultLogger().LogAsync("PTtoNormalTimeStamp processing string: " + PTTime);

          // break down the string into hours minutes and seconds
          Match match = Regex.Match(PTTime, "^PT(?:(?<hours>[0-9]+)H)?(?:(?<minutes>[0-9]+)M)?(?:(?<seconds>[0-9]+)S)?$");
          if (!match.Success) {
               new DefaultLogger().LogAsync($"failed to match timestamp: \"{PTTime}\". returning \"LIVE\"");
               return "LIVE";
          }

          string hoursStr = match.Groups["hours"].Value;
          string minutesStr = match.Groups["minutes"].Value;
          string secondsStr = match.Groups["seconds"].Value;

          int hours, minutes, seconds;
          if (hoursStr == null || !int.TryParse(hoursStr, out hours)) hours = 0;
          if (minutesStr == null || !int.TryParse(minutesStr, out minutes)) minutes = 0;
          if (secondsStr == null || !int.TryParse(secondsStr, out seconds)) seconds = 0;

          // minutes are formated differently depending on if there are hours
          string timestamp = "";
          if (hours > 0) {
               timestamp += $"{hoursStr}:";
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
          SearchResource.ListRequest? SearchRequest = YTService?.Search.List("snippet");
          if (SearchRequest == null) return null;

          SearchRequest.Q = query;
          SearchRequest.MaxResults = 5;

          SearchListResponse SearchResponse = await SearchRequest.ExecuteAsync();
          string? VideoID = null;

          for (int i = 0; i < SearchResponse.Items.Count; i++) {
               SearchResult result = SearchResponse.Items[i];
               if (result.Id.Kind == "youtube#video") {
                    VideoID = result.Id.VideoId;
                    break;
               }
          }
          if (VideoID == null) return null;
          return VideoID;
     }

     public async Task<ImmutableList<SearchResult>?> YTSearchResults(string query) {
          SearchResource.ListRequest? SearchRequest = YTService?.Search.List("snippet");
          if (SearchRequest == null) return null;
          SearchRequest.Q = query;

          SearchListResponse SearchResponse = await SearchRequest.ExecuteAsync();

          return SearchResponse.Items.ToImmutableList();
     }

     public string? GetYoutubeID(string url) {
          const string pattern = @"^(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:watch\?(?:.*&)?v=|embed\/|v\/|shorts\/)|youtu\.be\/)(?<videoId>[A-Za-z0-9_-]{11})(?:[?&].*)?$";
          Match match = Regex.Match(url, pattern);
          if (match.Success) {
               return match.Groups["videoId"].Value;
          } else {
               Logger.LogAsync("Invalid URL");
               return null; // rick roll video ID on failure
          }
     }
}