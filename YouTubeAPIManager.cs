using Google.Apis.Services;
using Google.Apis.YouTube.v3.Data;
using Google.Apis.YouTube.v3;
using System.Text.RegularExpressions;
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
          VideosResource.ListRequest? VideoRequest = YTService?.Videos.List("snippet, contentDetails, statistics");
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
          if (!match.Success) throw new ArgumentException();

          string hours, minutes, seconds;
          string timestamp = "";

          hours = match.Groups["hours"].Value;
          minutes = match.Groups["minutes"].Value;
          seconds = match.Groups["seconds"].Value;

          // minutes are formated differently depending on if there are hours
          if (!string.IsNullOrEmpty(hours)) {
               timestamp += $"{hours}:";
               timestamp += !string.IsNullOrEmpty(minutes) ? $"{minutes:00}:" : "00:";
          } else timestamp += !string.IsNullOrEmpty(minutes) ? $"{minutes:0}:" : "0:";

          // seconds are always presented with the same formatting
          if (!string.IsNullOrEmpty(seconds)) timestamp += $"{seconds:00}";

          return timestamp;
     }

     // this isnt really used since its so long
     public static string IDToURL(string VideoID) {
          return @"https://www.youtube.com/v/" + VideoID;
     }
}