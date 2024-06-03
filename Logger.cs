using Discord;
using Discord.Webhook;

public interface ILogger {
     public Task LogAsync(LogMessage message);
};

public class ComboLogger : ILogger {
     DiscordWebhookClient? WebhookClient;
     public ComboLogger (DiscordWebhookClient client) {
          WebhookClient = client;
     }

     public async Task LogAsync(LogMessage message) {
          Console.WriteLine($"[General/{message.Severity}] {message}");

          if (WebhookClient != null) await WebhookClient.SendMessageAsync($"```[nevetsCotsu] {message.ToString()}```");
     }
}

public class DefaultLogger : ILogger {
     public Task LogAsync(LogMessage message) {
          Console.WriteLine($"[General/{message.Severity}] {message}");
          return Task.CompletedTask;
     }
}


