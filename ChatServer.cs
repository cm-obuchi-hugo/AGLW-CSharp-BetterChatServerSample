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
    class ChatServer
    {
        public static int MessageLength = 256;
        public static int SleepDuration = 100; // 100ms

        // A UTF-8 encoder to process byte[] <-> string conversion
        public readonly System.Text.Encoding Encoder = System.Text.Encoding.UTF8;

        public GameSession ManagedGameSession { get; private set; } = null;

        // TCP lisenter has it's own thread
        private TcpListener listener = null;
        private Thread listenerThread = null;

        private Thread senderThread = null;
        private Thread retrieverThread = null;

        private ConcurrentQueue<byte[]> messagePool = null;

        private event Action<byte[]> sendMsgDelegate;

        // private Dictionary<int, string> playerSessions;
        List<ConnectedClient> clientPool = null;
        List<ConnectedClient> clientsInQueue = null;

        public ChatServer(GameSession session)
        {
            ManagedGameSession = session;

            messagePool = new ConcurrentQueue<byte[]>();
            clientPool = new List<ConnectedClient>();
            clientsInQueue = new List<ConnectedClient>();

            StartServer();
        }

        internal void UpdateGameSession(GameSession gameSession)
        {
            ManagedGameSession = gameSession;
        }

        public void StartServer()
        {
            // Create a TCP listener(in a listener thread) from port when when ProcessReady() returns success
            LaunchListenerThread(ManagedGameSession.Port);
            LaunchSenderThread();
            LaunchRetrieverThread();
        }

        // A method creates thread for listener
        void LaunchListenerThread(int port)
        {
            listenerThread = new Thread(() =>
                {
                    Listen(port);
                });

            listenerThread.Start();

            Console.WriteLine($"Server : Listener thread is created and started");
        }

        void LaunchSenderThread()
        {
            senderThread = new Thread(() => SendToAllClients());
            senderThread.Start();
        }

        void LaunchRetrieverThread()
        {
            retrieverThread = new Thread(() => RetrieveFromAllClients());
            retrieverThread.Start();
        }

        // A method listens the port.
        // When client connects : 
        // 1) Send msg to client -> 2) Wait msg from client -> 3) Close then connection and break
        void Listen(int port)
        {
            listener = TcpListener.Create(ManagedGameSession.Port);
            listener.Start();

            Console.WriteLine($"Server : Start listening port {port}");

            while (true)
            {
                // TcpClient.AccecptTcpClient() blocks
                TcpClient client = listener.AcceptTcpClient();
                ConnectedClient c = new ConnectedClient(client);
                sendMsgDelegate += c.SendMessage;
                c.StartClient();

                clientsInQueue.Add(c);
            }
        }


        private void SendToAllClients()
        {
            while (true)
            {
                SleepForAWhile();

                if (messagePool.Count > 0)
                {
                    byte[] bytes = new byte[MessageLength];
                    if (messagePool.TryDequeue(out bytes))
                    {
                        sendMsgDelegate(bytes);
                    }
                }
            }
        }

        private void RetrieveFromAllClients()
        {
            while (true)
            {
                if (clientsInQueue.Count > 0)
                {
                    clientPool.AddRange(clientsInQueue);
                    clientsInQueue.Clear();
                }

                SleepForAWhile();

                List<ConnectedClient> disconnectedClients = new List<ConnectedClient>();
                
                byte[] bytes = new byte[MessageLength];
                foreach (var c in clientPool)
                {
                    if (c != null && c.TargetClient.Connected)
                    {
                        if (c.RetrieveMessage(out bytes))
                        {
                            messagePool.Enqueue(bytes);
                        }
                    }
                    else
                    {
                        disconnectedClients.Add(c);
                    }
                }

                // Release disconnected client object
                foreach (var dc in disconnectedClients)
                {
                    clientPool.Remove(dc);
                }

            }
        }

        private void SleepForAWhile()
        {
            Thread.Sleep(SleepDuration);
        }
    }
}
