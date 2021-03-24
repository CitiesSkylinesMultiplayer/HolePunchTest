using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using LiteNetLib;

namespace Server
{
    /// <summary>
    ///     This is the UDP hole punching server, the clients will connect
    ///     to this server to setup the correct ports
    /// </summary>
    class Program : INatPunchListener
    {
        private NetManager _puncher;
        
        private static readonly TimeSpan KickTime = new(0, 0, 6);
        private readonly Dictionary<string, WaitPeer> _waitingPeers = new();
        private readonly List<string> _peersToRemove = new();
        
        private const int ServerPort = 8080;
        
        private void Run()
        {
            var netListener = new EventBasedNetListener();
            
            _puncher = new NetManager(netListener);
            _puncher.Start(ServerPort);
            _puncher.NatPunchEnabled = true;
            _puncher.NatPunchModule.Init(this);

            Console.WriteLine("Press ESC to quit");
            
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    break;
                
                var now = DateTime.Now;
                
                _puncher.NatPunchModule.PollEvents();
                
                // Check old peers
                foreach (var (key, peer) in _waitingPeers) 
                {
                    if (now - peer.RefreshTime > KickTime) 
                    {
                        _peersToRemove.Add(key);
                    }
                }

                // Remove peers that are due for removal
                for (var i = 0; i <= _peersToRemove.Count - 1; i++) 
                {
                    Console.WriteLine("Kicking peer: " + _peersToRemove[i]);
                    _waitingPeers.Remove(_peersToRemove[i]);
                }
                
                _peersToRemove.Clear();

                Thread.Sleep(10);
            }
            
            _puncher.Stop();
        }
        
        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            if (_waitingPeers.TryGetValue(token, out var waitPeer)) {
                if (waitPeer.InternalAddr.Equals(localEndPoint) && waitPeer.ExternalAddr.Equals(remoteEndPoint)) {
                    waitPeer.Refresh();
                    return;
                }

                Console.WriteLine("Wait peer found, sending introduction...");

                //found in list - introduce client and host to eachother
                Console.WriteLine("host - i({0}) e({1}) " + " client - i({2}) e({3})", waitPeer.InternalAddr, waitPeer.ExternalAddr, localEndPoint, remoteEndPoint);

                // host internal
                // host external
                // client internal
                // client external
                // request token
                _puncher.NatPunchModule.NatIntroduce(waitPeer.InternalAddr, waitPeer.ExternalAddr, localEndPoint, remoteEndPoint, token);

                // No longer waiting
                _waitingPeers.Remove(token);
            } else {
                Console.WriteLine("Wait peer created. i({0}) e({1})", localEndPoint, remoteEndPoint);
                _waitingPeers[token] = new WaitPeer(localEndPoint, remoteEndPoint);
            }
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            // Ignore as we are the server
        }
        
        static void Main(string[] args) => new Program().Run();
        
        private class WaitPeer
        {
            public IPEndPoint InternalAddr { get; }

            public IPEndPoint ExternalAddr { get; }

            public DateTime RefreshTime { get; private set; }

            public void Refresh()
            {
                RefreshTime = DateTime.Now;
            }

            public WaitPeer(IPEndPoint internalAddress, IPEndPoint externalAddress)
            {
                Refresh();
                InternalAddr = internalAddress;
                ExternalAddr = externalAddress;
            }
        }
    }
}