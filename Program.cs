using System;

namespace AGLW_CSharp_BetterChatServerSample
{
    class Program
    {
        static private GameLiftServer gameLiftServer = new GameLiftServer(); 
        static void Main(string[] args)
        {
            gameLiftServer.Start();

            while(gameLiftServer.IsAlive)
            {

            }

            Console.WriteLine("Program ends.");
        }
    }
}
