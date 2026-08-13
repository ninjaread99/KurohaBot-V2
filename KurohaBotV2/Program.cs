#define VERBOSE_FILE
#define VERBOSE_CONSOLE

//#define SELF_TEST

#warning remember to update version when needed!
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.Util.Store;

using Youtube.Api.V3;

using Serilog;

using Grpc.Core;
using Grpc.Net.Client;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Security;
using System.Reflection;
using KurohaBotV2;



public static class Bot
{
    enum ExitCodes
    {
        StreamNotFound = -1,
        Success = 0
    }

    public static YouTubeService youtube { get; private set; }
    public static Google.Apis.YouTube.v3.Data.LiveBroadcast stream;
    private static UserCredential youtubeCredential;

    internal static ChatQueue queue { get; private set; } = new ChatQueue();

    public static void Main(string[] args)
    {
        Serilog.Events.LogEventLevel ConsoleLevel = Serilog.Events.LogEventLevel.Information;
        Serilog.Events.LogEventLevel FileLevel = Serilog.Events.LogEventLevel.Information;
#if DEBUG
        ConsoleLevel = Serilog.Events.LogEventLevel.Debug;
        FileLevel = Serilog.Events.LogEventLevel.Debug;
#if VERBOSE_CONSOLE
        ConsoleLevel = Serilog.Events.LogEventLevel.Verbose;
#endif
#if VERBOSE_FILE
        FileLevel = Serilog.Events.LogEventLevel.Verbose;
#endif
#endif

#if !INPUT_VIA_CONSOLE
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.File("Log.log", restrictedToMinimumLevel: FileLevel).WriteTo.Console(restrictedToMinimumLevel: ConsoleLevel, outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}").CreateLogger();
#else
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.File("Log.log", restrictedToMinimumLevel: FileLevel).CreateLogger();
#endif

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

        Log.Information("Started!");
        Log.Information($"Version: {version}");
        Log.Information($"Console log level: {ConsoleLevel}");
        Log.Information($"File log level: {FileLevel}");
        Run(args).GetAwaiter().GetResult();
    }

    public static async Task Run(string[] args)
    {

        InitializeYouTubeService();

        var request = youtube.LiveBroadcasts.List("snippet,status");
        request.Mine = true;

         var activeStreams = await request.ExecuteAsync(); //once threw something about expired token, rerunning fixed. Fix later I guess.

        var allStreams = activeStreams.Items.ToList();

#if SELF_TEST
        string testMessageOne = "This is a test message. It is here only to test the functionality of splitting messages";
        string testMessageTwo = "This is a test message. It is here only to test the functionality of splitting messages. This message is longer than the previous one, and should be split into multiple messages when sent to the chat. This is a test message. It is here only to test the functionality of splitting messages. This message is longer than the previous one, and should be split into multiple messages when sent to the chat.";

        string testMessageThree = "";

        for (int i = 0; i <10; i++)
        {
            testMessageThree += "This is a long test message. Like, really long. It is here only to test the functionality of splitting messages. This message is longer than the previous one, and should be split into multiple messages when sent to the chat. This is a long test message. Like, really long. It is here only to test the functionality of splitting messages. This message is longer than the previous one, and should be split into multiple messages when sent to the chat.";
        }

        string testMessageFour = "";

        for (int i = 0; i < 10; i++)
        {
            testMessageFour += "ThisIsALongTestMessageWithNoSpaces.ItIsHereOnlyToTestTheFunctionalityOfSplittingMessages.ThisMessageShouldStillBeSplitInToMultipleParts,ButItWon'tBeClean";
        }

        string[] mArr = MessageHandler.SplitText(testMessageOne);
        bool fits = true;
        foreach (var message in mArr)
        {
            Console.WriteLine($"Test message one part: {message}");
            if (message.Length > 200)
            {
                fits = false;
            }
        }
        Console.WriteLine(testMessageOne.Length);
        Console.WriteLine(fits);
        fits = true;
        mArr = MessageHandler.SplitText(testMessageTwo);
        foreach (var message in mArr)
        {
            Console.WriteLine($"Test message two part: {message}");
        }
        Console.WriteLine(testMessageTwo.Length);
        Console.WriteLine(fits);
        fits = true;
        mArr = MessageHandler.SplitText(testMessageThree);
        foreach (var message in mArr)
        {
            Console.WriteLine($"Test message three part: {message}");
        }
        Console.WriteLine(testMessageThree.Length);
        Console.WriteLine(fits);
        fits = true;
        mArr = MessageHandler.SplitText(testMessageFour);
        foreach (var message in mArr)
        {
            Console.WriteLine($"Test message four part: {message}");
        }
        Console.WriteLine(testMessageFour.Length);
        Console.WriteLine(fits);

        return;
#endif


        if (allStreams.Count == 0)
        {
            Exit(ExitCodes.StreamNotFound, $"Either an error occurred getting streams, or no streams are currently in the \"Ready\", \"Testing\", or \"Live\" internal states. Please check the state of your current stream.");
        }
        var Streams = allStreams.Where(s => s.Status.LifeCycleStatus == "ready" ||
        s.Status.LifeCycleStatus == "testing" || s.Status.LifeCycleStatus == "live").ToList();
        if (Streams.Count == 0)
        {
            Log.Fatal($"No valid streams were found. Stream must be in the internal states of  \"Ready\", \"Testing\", or \"Live\"");
            //Log.Debug($"allStreams: {string.Join(", ", allStreams.Items.Select(s => $"{s.Snippet.Title}: {s.Status?.LifeCycleStatus}"))}\nStreams: {string.Join(", ", Streams.Items.Select(s => $"{s.Snippet.Title}: {s.Status?.LifeCycleStatus}"))}");
            Environment.Exit((int)ExitCodes.StreamNotFound);
        }

        var streamLocal = Streams[0];

        if (Streams.Count == 1)
        {
            streamLocal = Streams[0];
        }
        else if (Streams.Count > 1)
        {
            string multipleStreamsMessage = "Multiple streams have been found. Please select which to use:";
            Console.WriteLine(multipleStreamsMessage);
            for (int i = 0; i < Streams.Count; i++)
            {
                Console.WriteLine($"{i + 1} {Streams[i].Snippet.Title}");
            }
            string? input = Console.ReadLine();
            int StreamNum;
            bool parseCheck = !int.TryParse(input, out StreamNum);
            bool boundsCheck = StreamNum - 1 > 0 && StreamNum - 1 < Streams.Count - 1;
            while (parseCheck && boundsCheck)
            {
                Console.Clear();
                if (!parseCheck)
                {
                    Console.WriteLine("Invalid input (not a number)");
                }
                else if (!boundsCheck)
                {
                    Console.WriteLine($"{StreamNum} is not between 1 and {Streams.Count}");
                }

                Console.WriteLine(multipleStreamsMessage);
                for (int i = 0; i < Streams.Count; i++)
                {
                    Console.WriteLine($"{i + 1} {Streams[i].Snippet.Title}");
                }
                input = Console.ReadLine();
                parseCheck = !int.TryParse(input, out StreamNum);
                boundsCheck = StreamNum - 1 > 0 && StreamNum - 1 < Streams.Count - 1;
            }
            streamLocal = Streams[StreamNum - 1];
        }
        Log.Debug($"Stream: {streamLocal.Snippet.Title}");

        stream = streamLocal;

        var id = streamLocal.Snippet.LiveChatId;

        var channel = GrpcChannel.ForAddress("https://youtube.googleapis.com");

        var client = new V3DataLiveChatMessageService
            .V3DataLiveChatMessageServiceClient(channel);

        var token = await youtubeCredential.GetAccessTokenForRequestAsync();

        var metadata = new Grpc.Core.Metadata
{
    { "authorization", $"Bearer {token}" }
};


        Log.Debug("Entering chat stream");


        string? pageToken = null;

        _ = Task.Run(ReadConsole);

        while (true)
        {
            //Log.Debug("Loop");
            var chatRequest = new LiveChatMessageListRequest
            {
                LiveChatId = id
            };
            if (!(pageToken == null))
            {
                chatRequest = new LiveChatMessageListRequest
                {
                    LiveChatId = id,
                    PageToken = pageToken
                };
            }

            chatRequest.Part.Add("snippet");
            chatRequest.Part.Add("authorDetails");

            using var call = client.StreamList(chatRequest, metadata);


            await foreach (var response in call.ResponseStream.ReadAllAsync())
            {
                pageToken = response.NextPageToken;
                foreach (var message in response.Items)
                {
                    Log.Information(
                        "{Author}: {Message}",
                        message.AuthorDetails.DisplayName,
                        message.Snippet.DisplayMessage
                    );
                    MessageHandler.HandleMessage(message);
                }
                
            }

        }


    }
        
    private static async Task ReadConsole()
    {
        while (true)
        {
            string? input = await Console.In.ReadLineAsync();
            if (input == null)
            {
                continue;
            }
            Log.Verbose($"Console input: {input}");
            ConsoleHandler.HandleConsoleCommand(input);
        }
    }

    private static void InitializeYouTubeService()
    {
        Log.Debug("Initializing YouTube service...");
        UserCredential credential;
        using (var stream = new FileStream("Secret.json", FileMode.Open, FileAccess.Read))
        {
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                new[] { YouTubeService.Scope.Youtube },
                "user",
                CancellationToken.None,
                new FileDataStore("YouTube.Auth.Store")).Result;
        }
        youtube = new YouTubeService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "KurohaBotV2"
        });
        youtubeCredential = credential;
        Log.Debug($"Youtube service: {youtube}");
    }

    private static void Exit(ExitCodes exitCode = ExitCodes.Success, string? message = null)
    {
        if (exitCode == ExitCodes.Success)
        {
            Log.Information(message??"No exit message provided");
        }
        else
        {
            message = message ?? "No exit reason provided.";
            message += " If the problem persists, please contact @ninjaread99 on Discord";
            Log.Fatal(message);
        }
        Environment.Exit((int)exitCode);
    }
}


class Chat
{
    public string id { get; private set; }
    private UserCredential credential;

    public Chat(string id, UserCredential credential)
    {
        this.id = id;
        this.credential = credential;
        
    }
}