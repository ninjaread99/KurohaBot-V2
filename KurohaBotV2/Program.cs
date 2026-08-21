#if DEBUG
#define VERBOSE_CONSOLE
#endif

#define VERBOSE_FILE


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
    const string version = "2.0.1";
#warning Don't forget to update the version!

    internal enum ExitCode
    {
        StreamNotFound = -1,
        Success = 0,
        StreamEnded = 1
    }

    internal static BotState state { get; private set; } = new BotState();

    public static YouTubeService youtube { get; private set; }
    public static Google.Apis.YouTube.v3.Data.LiveBroadcast stream;
    private static UserCredential youtubeCredential;

    internal static ChatQueue queue { get; private set; } = new ChatQueue();
    internal static string? NextPageToken { get; private set; }

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
#endif
#if VERBOSE_FILE
        FileLevel = Serilog.Events.LogEventLevel.Verbose;
#endif

        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.File("Log.log", restrictedToMinimumLevel: FileLevel).WriteTo.Console(restrictedToMinimumLevel: ConsoleLevel, outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}").CreateLogger();
        //#if !INPUT_VIA_CONSOLE
        //        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.File("Log.log", restrictedToMinimumLevel: FileLevel).WriteTo.Console(restrictedToMinimumLevel: ConsoleLevel, outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}").CreateLogger();
        //#else
        //        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.File("Log.log", restrictedToMinimumLevel: FileLevel).CreateLogger();
        //#endif

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

        var activeStreams = await request.ExecuteAsync(); //twice threw something about expired token, rerunning fixed. Fix later I guess.

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
            Exit(ExitCode.StreamNotFound, $"Either an error occurred getting streams, or no streams are currently in the \"Ready\", \"Testing\", or \"Live\" internal states. Please check the state of your current stream.");
        }
        var Streams = allStreams.Where(s => s.Status.LifeCycleStatus == "ready" ||
        s.Status.LifeCycleStatus == "testing" || s.Status.LifeCycleStatus == "live").ToList();
        if (Streams.Count == 0)
        {
            Log.Fatal($"No valid streams were found. Stream must be in the internal states of  \"Ready\", \"Testing\", or \"Live\"");
            //Log.Debug($"allStreams: {string.Join(", ", allStreams.Items.Select(s => $"{s.Snippet.Title}: {s.Status?.LifeCycleStatus}"))}\nStreams: {string.Join(", ", Streams.Items.Select(s => $"{s.Snippet.Title}: {s.Status?.LifeCycleStatus}"))}");
            Environment.Exit((int)ExitCode.StreamNotFound);
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

        string StreamId = streamLocal.Id;
        NextPageToken = null;
        AttemptStateLoad(StreamId, out string? TempNextPageToken);
        NextPageToken = TempNextPageToken;

        var ChatId = streamLocal.Snippet.LiveChatId;

        var channel = GrpcChannel.ForAddress("https://youtube.googleapis.com");

        var client = new V3DataLiveChatMessageService
            .V3DataLiveChatMessageServiceClient(channel);

        var token = await youtubeCredential.GetAccessTokenForRequestAsync();

        var metadata = new Grpc.Core.Metadata
{
    { "authorization", $"Bearer {token}" }
};


        Log.Debug("Entering chat stream");

        _ = Task.Run(ReadConsole);

        var StillLiveRequest = youtube.LiveBroadcasts.List("status");
        StillLiveRequest.Id = stream.Id;

        ulong iterations = 0;

        Log.Information("Ready!");

        //Main loop
        while (true)
        {
            iterations++;

            if (iterations%3==0)
            {
                var result = await StillLiveRequest.ExecuteAsync();
                var currentStream = result.Items.FirstOrDefault();
                if (currentStream == null || currentStream.Status.LifeCycleStatus is not "live" or "ready" or "testing")
                {
                    Log.Information("Stream appears to have ended. Saving status and exiting.");
                    state.Save();
                    Exit(ExitCode.StreamEnded);
                }
            }

            //Log.Debug("Loop");
            var chatRequest = new LiveChatMessageListRequest
            {
                LiveChatId = ChatId
            };
            if (!(NextPageToken == null))
            {
                chatRequest = new LiveChatMessageListRequest
                {
                    LiveChatId = ChatId,
                    PageToken = NextPageToken
                };
            }

            chatRequest.Part.Add("snippet");
            chatRequest.Part.Add("authorDetails");

            using var call = client.StreamList(chatRequest, metadata);


            await foreach (var response in call.ResponseStream.ReadAllAsync())
            {
                NextPageToken = response.NextPageToken;
                state.Save();
                foreach (var message in response.Items)
                {
                    Log.Debug(
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

    private static void AttemptStateLoad(string? StreamId, out string? NextPageToken)
    {
        NextPageToken = null;
        Log.Information($"Attempting to load...");
        BotState? state = BotState.Load();
        if (state == null)
        {
            Log.Information($"No save found, performing initialization...");
            queue = new ChatQueue();
            Log.Information($"Initialized!");
        }
        else if (state.StreamId != StreamId)
        {
            Log.Information($"Found a save file, but it appears to be for another stream. Use it anyway? (y/n)");
            Console.Write(">>");
            string? input = Console.ReadLine().ToLower();
            Log.Debug($"Response (lowercase): {input}");
            bool ValidInput = input == "y" || input == "n";
            while (!ValidInput)
            {
                Console.WriteLine("y/n only");
                Console.Write(">>");
                input = Console.ReadLine().ToLower();
                Log.Debug($"Response (lowercase): {input}");
                ValidInput = input == "y" || input == "n";
            }
            if (input == "y")
            {
                queue = state.queue;
                //intentionally omit nextPageToken, as it's for another stream.
            }
            else
            {
                queue = new ChatQueue();
            }
        }
        else
        {
            Log.Information($"Found a save file! Use it? (y/n)");
            Console.Write(">>");
            string? input = Console.ReadLine().ToLower();
            Log.Debug($"Response (lowercase): {input}");
            bool ValidInput = input == "y" || input == "n";
            while (!ValidInput)
            {
                Console.WriteLine("y/n only");
                Console.Write(">>");
                input = Console.ReadLine().ToLower();
                Log.Debug($"Response (lowercase): {input}");
                ValidInput = input == "y" || input == "n";
            }

            if (input == "y")
            {
                queue = state.queue;
                NextPageToken = state.NextPageToken;
            }
            else
            {
                queue = new ChatQueue();
            }
        }
    }

    internal static void Exit(ExitCode exitCode = ExitCode.Success, string? message = null)
    {
        if (exitCode == ExitCode.Success)
        {
            Log.Information(message??"No exit message provided");
        }
        else if (exitCode < 0)
        {
            message = message ?? "No exit reason provided.";
            message += " If the problem persists, please contact @ninjaread99 on Discord";
            Log.Fatal(message);
        }
        else if (exitCode > 0)
        {
            message = message ?? "No exit reason provided.";
            message += " If this happened unintentionally,, please contact @ninjaread99 on Discord";
            Log.Information(message);
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