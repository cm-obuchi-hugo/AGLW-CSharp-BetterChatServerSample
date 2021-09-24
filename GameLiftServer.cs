using System;
using System.Collections.Generic;

using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;

namespace AGLW_CSharp_BetterChatServerSample
{
    class GameLiftServer
    {
        public Dictionary<string, ChatServer> Servers { get; private set; } = null;

        // A instance's status flag of this class 
        public bool IsAlive { get; private set; } = false;

        public GameLiftServer()
        {
            Servers = new Dictionary<string, ChatServer>();
        }

        //This is an example of a simple integration with GameLift server SDK that will make game server processes go active on GameLift!
        public void Start()
        {
            //Identify port number (hard coded here for simplicity) the game server is listening on for player connections
            var listeningPort = 7777;

            //InitSDK will establish a local connection with GameLift's agent to enable further communication.
            Console.WriteLine(GameLiftServerAPI.GetSdkVersion().Result);

            var initSDKOutcome = GameLiftServerAPI.InitSDK();
            if (initSDKOutcome.Success)
            {
                ProcessParameters processParameters = new ProcessParameters(
                    this.OnStartGameSession,
                    this.OnUpdateGameSession,
                    this.OnProcessTerminate,
                    this.OnHealthCheck,
                    listeningPort, //This game server tells GameLift that it will listen on port 7777 for incoming player connections.
                    new LogParameters(new List<string>()
                    {
                    //Here, the game server tells GameLift what set of files to upload when the game session ends.
                    //GameLift will upload everything specified here for the developers to fetch later.
                    "/local/game/logs/myserver.log"
                    }));

                //Calling ProcessReady tells GameLift this game server is ready to receive incoming game sessions!
                var processReadyOutcome = GameLiftServerAPI.ProcessReady(processParameters);
                if (processReadyOutcome.Success)
                {
                    // Set Server to alive when ProcessReady() returns success
                    IsAlive = true;

                    Console.WriteLine("ProcessReady success.");
                }
                else
                {
                    IsAlive = false;
                    Console.WriteLine("ProcessReady failure : " + processReadyOutcome.Error.ToString());
                }
            }
            else
            {
                IsAlive = true;
                Console.WriteLine("InitSDK failure : " + initSDKOutcome.Error.ToString());
            }
        }

        void OnStartGameSession(GameSession gameSession)
        {
            ChatServer server = new ChatServer(gameSession);
            Servers.Add(gameSession.GameSessionId, server);

            //When a game session is created, GameLift sends an activation request to the game server and passes along the game session object containing game properties and other settings.
            //Here is where a game server should take action based on the game session object.
            //Once the game server is ready to receive incoming player connections, it should invoke GameLiftServerAPI.ActivateGameSession()
            Console.WriteLine($"Server : OnStartGameSession() called");
            GameLiftServerAPI.ActivateGameSession();
        }

        void OnUpdateGameSession(UpdateGameSession updateGameSession)
        {
            ChatServer server = null;
            if (Servers.TryGetValue(updateGameSession.GameSession.GameSessionId, out server))
            {
                server.UpdateGameSession(updateGameSession.GameSession);
                // Do sth more for server?
            }

            //When a game session is updated (e.g. by FlexMatch backfill), GameLiftsends a request to the game
            //server containing the updated game session object.  The game server can then examine the provided
            //matchmakerData and handle new incoming players appropriately.
            //updateReason is the reason this update is being supplied.
            Console.WriteLine($"Server : OnUpdateGameSession() called");
        }

        void OnProcessTerminate()
        {
            //OnProcessTerminate callback. GameLift will invoke this callback before shutting down an instance hosting this game server.
            //It gives this game server a chance to save its state, communicate with services, etc., before being shut down.
            //In this case, we simply tell GameLift we are indeed going to shutdown.
            Console.WriteLine($"Server : OnProcessTerminate() called");
            GameLiftServerAPI.ProcessEnding();

            IsAlive = false;
        }

        bool OnHealthCheck()
        {
            //This is the HealthCheck callback.
            //GameLift will invoke this callback every 60 seconds or so.
            //Here, a game server might want to check the health of dependencies and such.
            //Simply return true if healthy, false otherwise.
            //The game server has 60 seconds to respond with its health status. GameLift will default to 'false' if the game server doesn't respond in time.
            //In this case, we're always healthy!
            Console.WriteLine($"Server : OnHealthCheck() called");
            return true;
        }

        void OnApplicationQuit()
        {
            //Make sure to call GameLiftServerAPI.Destroy() when the application quits. This resets the local connection with GameLift's agent.
            GameLiftServerAPI.Destroy();

            IsAlive = false;
        }
    }
}