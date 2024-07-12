using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using DotNetEnv;
using Serilog;
using Serilog.Enrichers.CallerInfo;

class Program {
     private static IServiceProvider? ServiceProvider;

     private static readonly DiscordSocketConfig SocketConfig = new()
     {
          GatewayIntents = GatewayIntents.GuildVoiceStates | GatewayIntents.GuildMembers | GatewayIntents.Guilds,
          AlwaysDownloadUsers = true,
          LogLevel = LogSeverity.Debug,
     };

     private static readonly InteractionServiceConfig ServiceConfig = new() {
          LogLevel = LogSeverity.Debug,
     };

     public static async Task Main(string[] args) {
          Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .Enrich.WithCallerInfo(includeFileInfo: true, assemblyPrefix: "dotnetDiscordBot", filePathDepth: 3)
               .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}][{Method}] {Message}{NewLine}{Exception}")
               .CreateLogger();

          // TestFunction();
          // return;

          // Check and Set Env variables.
          Env.Load();
          string? DISCORD_TOKEN = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
          if (string.IsNullOrEmpty(DISCORD_TOKEN)) {
               Log.Fatal("Failed to acquire DISCORD_TOKEN as an environment variable");
               return;
          }

          // Setup Dependency Injection and Initialize Services
          ServiceProvider = new ServiceCollection()
               .AddSingleton<DiscordSocketClient>(_ => new DiscordSocketClient(SocketConfig))
               .AddSingleton<YTAPIManager>(_ => new YTAPIManager())
               .AddSingleton<YTSearchAutocomplete>()
               .AddSingleton<InteractionService>(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>().Rest, ServiceConfig))
               .AddSingleton<InteractionHandler>()
               .AddSingleton<ConcurrentDictionary<ulong, GuildData>>()
               .BuildServiceProvider();

          DiscordSocketClient SocketClient = ServiceProvider.GetRequiredService<DiscordSocketClient>();
          await SocketClient.SetGameAsync("Adwin", type: ActivityType.Watching);

          // Initialize Interaction Handler
          await ServiceProvider.GetRequiredService<InteractionHandler>().InitializeAsync();

          // Connect to Discord Gateway
          await SocketClient.LoginAsync(TokenType.Bot, DISCORD_TOKEN);
          await SocketClient.StartAsync();

          await Task.Delay(Timeout.Infinite);
     }

     private static void TestFunction() {
          LinkedList<TestClass> list = new();

          list.AddFirst(new TestClass(0));
          list.AddFirst(new TestClass(1));
          list.AddFirst(new TestClass(2));
          list.AddFirst(new TestClass(3));
          list.AddFirst(new TestClass(4));

          LinkedListNode<TestClass> first = list.First!;
          LinkedListNode<TestClass> third = list.First!.Next!.Next!;


          var tmp = first.Value;
          first.Value = third.Value;
          third.Value = tmp;

          var Enum = list.Last;

          while (Enum != null) {
               Log.Debug(Enum.Value.Val.ToString());
               Enum = Enum.Previous;
          }
     }

     public class TestClass {
          public int Val;

          public TestClass (int val) {
               Val = val;
          }
     }
}