using Discord;
using Discord.Interactions;
using Discord.WebSocket;

public class TestModule : InteractionModuleBase<SocketInteractionContext> {
     [SlashCommand("test", "for testing")]
     public async Task Test(SocketGuildUser user) {
          if ( user.IsMuted ) await RespondAsync("User is muted"); 
          else await RespondAsync("User is not muted");
     }
}