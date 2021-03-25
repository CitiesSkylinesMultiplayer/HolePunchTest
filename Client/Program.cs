using System;

namespace Client
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the testing program. Who do you want to run as?");
            Console.WriteLine("[0] Client");
            Console.WriteLine("[1] Server");

            switch (Console.ReadKey().KeyChar)
            {
                case '0':
                    new CClient().Run();
                    return;
                case '1':
                    new CServer().Run();
                    return;
            }
            
            Console.WriteLine("Invalid Selection!");
        }
    }

    public abstract class CBase
    {
        protected string RelayServerIp;
        
        public abstract void Run();

        protected void RequestRelayServerIp()
        {
            Console.WriteLine("Enter IP Address of relay server: ");
            RelayServerIp = Console.ReadLine();
        }
    }
    
    public class CClient : CBase
    {
        public override void Run()
        {
            Console.WriteLine("Stating Client...");
            RequestRelayServerIp();
            
        }
    }

    public class CServer : CBase
    {
        public override void Run()
        {
            Console.WriteLine("Stating Server...");
            RequestRelayServerIp();
            
        }
    }
}