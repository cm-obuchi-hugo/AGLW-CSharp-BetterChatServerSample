using System;
using System.IO;
using System.Collections.Generic;


using System.Collections.Concurrent;
using System.Threading;
using System.Net;
using System.Net.Sockets;


using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;

namespace AGLW_CSharp_BetterChatServerSample
{
    class ConnectedClient
    {
        public readonly System.Text.Encoding Encoder = System.Text.Encoding.UTF8;

        public TcpClient TargetClient { get; private set; } = null;
        public NetworkStream TargetStream { get; private set; } = null;

        public ConcurrentQueue<byte[]> SendingQueue { get; private set; } = null;
        public ConcurrentQueue<byte[]> ReceivingQueue { get; private set; } = null;

        public Thread SenderThread { get; private set; } = null;
        public Thread ReceiverThread { get; private set; } = null;

        public ConnectedClient(TcpClient client)
        {
            TargetClient = client;
            TargetStream = client.GetStream();

            SendingQueue = new ConcurrentQueue<byte[]>();
            ReceivingQueue = new ConcurrentQueue<byte[]>();
        }

        public void StartClient()
        {
            SenderThread = new Thread(() => Send());
            SenderThread.Start();

            ReceiverThread = new Thread(() => Receive());
            ReceiverThread.Start();
        }

        // Should be called by server, add a message to queue, will be sent to client later
        public void SendMessage(byte[] bytes)
        {
            SendingQueue.Enqueue(bytes);
        }

        // Should be called by server, retrieve message
        public bool RetrieveMessage(out byte[] bytes)
        {
            bool retrieved = ReceivingQueue.TryDequeue(out bytes);

            return retrieved;
        }

        // Looping in a sender thread
        private void Send()
        {
            byte[] bytes;

            while (TargetStream != null)
            {
                if (SendingQueue.TryDequeue(out bytes))
                {
                    TargetStream.Write(bytes);
                }
            }
        }

        // Looping in a receiver thread
        private void Receive()
        {
            byte[] bytes = new byte[ChatServer.MessageLength];

            if (TargetStream != null)
            {
                try
                {
                    while (TargetStream.Read(bytes) > 0)
                    {
                        Console.WriteLine($"Message Received: {Encoder.GetString(bytes)}");
                        ReceivingQueue.Enqueue(bytes);
                        bytes = new byte[ChatServer.MessageLength];
                    }
                }
                catch(SocketException e)
                {
                    Console.WriteLine($"Excpetion catched : {e}");
                }
                catch(IOException e)
                {
                    Console.WriteLine($"Excpetion catched : {e}");
                }
            }
        }
    }
}