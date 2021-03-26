using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;

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
        
        protected NetManager Net;
        
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
            var connected = false;
            NetPeer? peer = null;
            
            Console.WriteLine("Stating Client...");
            RequestRelayServerIp();

            var serverIp = RequestServerIp();
            
            var globalServer = new IPEndPoint(IPAddress.Parse(RelayServerIp), 4240);

            var natPunchListener = new EventBasedNatPunchListener();
            natPunchListener.NatIntroductionSuccess += (point, _, _) =>
            {
                Console.WriteLine("Nat Introduction Success, Connecting to " + point);
                peer = Net.Connect(point, "CSM");
                connected = true;
            };
            
            var netListener = new EventBasedNetListener();
            netListener.NetworkReceiveEvent += (peer, reader, method) =>
            {
                var message = reader.GetString();
                Console.WriteLine($"[{peer}] {message}");
            };

            netListener.PeerConnectedEvent += netPeer =>
            {
                Console.WriteLine("Connected to server: " + netPeer.EndPoint);
            };
            
            Net = new NetManager(netListener);
            Net.NatPunchEnabled = true;
            Net.UnconnectedMessagesEnabled = true;
            Net.NatPunchModule.Init(natPunchListener);

            Net.Start();
            Net.NatPunchModule.SendNatIntroduceRequest(globalServer, $"client_{serverIp}");
            
            var running = true;
            while (running)
            {
                Net.NatPunchModule.PollEvents();
                Net.PollEvents();
                
                // Wait till connected
                if (peer == null)
                {
                    Thread.Sleep(100);
                    continue;
                }
                
                Console.WriteLine("Ping:");
                peer.Send(NetDataWriter.FromString("Ping"), DeliveryMethod.Unreliable);
                
                Thread.Sleep(100);
            }
            
            Net.Stop();
        }
        
        private string RequestServerIp()
        {
            Console.WriteLine("Enter IP Address of server: ");
            return Console.ReadLine();
        }
    }

    public class CServer : CBase
    {
        public override void Run()
        {
            Console.WriteLine("Stating Server...");
            RequestRelayServerIp();

            var globalServer = new IPEndPoint(IPAddress.Parse(RelayServerIp), 4240);

            var natPunchListener = new EventBasedNatPunchListener();
            natPunchListener.NatIntroductionSuccess += (point, _, _) =>
            {
                Console.WriteLine("Nat Introduction Success, Accepting connection from " + point);
            };
            
            var netListener = new EventBasedNetListener();
            netListener.NetworkReceiveEvent += (peer, reader, method) =>
            {
                Console.WriteLine("Server received: " + reader.GetString());
                peer.Send(NetDataWriter.FromString("Pong from server!"), DeliveryMethod.ReliableOrdered);
            };

            netListener.PeerConnectedEvent += peer =>
            {
                Console.WriteLine("Client connected to server: " + peer.EndPoint);
            };

            netListener.ConnectionRequestEvent += request =>
            {
                Console.WriteLine("Connection request from: " + request.RemoteEndPoint);
                request.AcceptIfKey("CSM");
            };
            
            Net = new NetManager(netListener);
            Net.NatPunchEnabled = true;
            Net.UnconnectedMessagesEnabled = true;
            Net.NatPunchModule.Init(natPunchListener);

            Net.Start(4230);
            
            Net.NatPunchModule.SendNatIntroduceRequest(globalServer, "server_a7Gd3H");

            var running = true;
            while (running)
            {
                Net.NatPunchModule.PollEvents();
                Net.PollEvents();

                // This would be sent every 5 seconds in the actual game
                Net.SendUnconnectedMessage(NetDataWriter.FromString("server_a7Gd3H"), globalServer);
                
                Thread.Sleep(100);
            }
            
            Net.Stop();
        }
    }
}