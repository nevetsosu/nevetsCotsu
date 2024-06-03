using Discord.Interactions;

public class CommandModules : InteractionModuleBase {
     [SlashCommand("ping", "respond with pong")]
     public async Task Ping() {
          await RespondAsync("pong!");
     }
}