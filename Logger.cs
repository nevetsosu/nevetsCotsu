// //
// // This file is not going to be used for now
// //
// using Discord;
// using Discord.Webhook;

// public interface ILogger {
//      public Task LogAsync(LogMessage message);
//      public Task LogAsync (string message);
// };

// public abstract class BaseLogger : ILogger {
//      public virtual Task LogAsync(string message) {
//           Console.WriteLine($"[Debug/Log] {message}");
//           return Task.CompletedTask;
//      }

//      public abstract Task LogAsync(LogMessage message);
// }

// public class ComboLogger : BaseLogger {
//      DiscordWebhookClient WebhookClient;
//      public ComboLogger (DiscordWebhookClient client) {
//           WebhookClient = client;
//      }

//      public override async Task LogAsync(LogMessage message) {
//           Console.WriteLine($"[General/{message.Severity}] {message}");

//           if (WebhookClient != null) await WebhookClient.SendMessageAsync($"```[nevetsCotsu] {message.ToString()}```");
//      }
// }

// public class DefaultLogger : BaseLogger {
//      public override Task LogAsync(LogMessage message) {
//           Console.WriteLine($"[General/{message.Severity}] {message}");
//           return Task.CompletedTask;
//      }
// }


