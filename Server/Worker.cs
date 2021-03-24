using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Net;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace Server
{
    public class Worker : BackgroundService, INatPunchListener
    {
        private NetManager _puncher;
        private readonly ILogger _logger;
        
        private static readonly TimeSpan KickTime = new(0, 0, 6);
        private readonly Dictionary<string, WaitPeer> _waitingPeers = new();
        private readonly List<string> _peersToRemove = new();
        
        private const int ServerPort = 8080;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }
        
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Setup 
            var netListener = new EventBasedNetListener();
            
            _puncher = new NetManager(netListener);
            _puncher.Start(ServerPort);
            _puncher.NatPunchEnabled = true;
            _puncher.NatPunchModule.Init(this);

            _logger.LogInformation("Starting NAT Relay Server...");
            
            // Loop
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                
                _puncher.NatPunchModule.PollEvents();
                
                // Check old peers, if a peer has been waiting for longer than
                // "KickTime", they need to be removed from the peer list
                foreach (var (key, peer) in _waitingPeers) 
                {
                    if (now - peer.RefreshTime > KickTime) 
                    {
                        _peersToRemove.Add(key);
                    }
                }

                // Now actually Remove peers that are due for removal
                for (var i = 0; i <= _peersToRemove.Count - 1; i++) 
                {
                    _logger.LogInformation("Kicking peer: {Peer}", _peersToRemove[i]);
                    _waitingPeers.Remove(_peersToRemove[i]);
                }
                
                _peersToRemove.Clear();

                Thread.Sleep(10);
            }
            
            _logger.LogInformation("Stopping NAT Relay Server...");
            
            _puncher.Stop();
            return Task.CompletedTask;
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            // See if the the other peer is in the waiting list
            if (_waitingPeers.TryGetValue(token, out var waitPeer)) 
            {
                // If the same client already waiting, refresh
                if (waitPeer.InternalAddr.Equals(localEndPoint) && waitPeer.ExternalAddr.Equals(remoteEndPoint)) 
                {
                    _logger.LogInformation("Existing peer, refreshing...");
                    waitPeer.Refresh();
                    return;
                }
                
                // At this point, we have access to both peers that want to connect to each other
                _logger.LogInformation("Wait peer found, sending introduction...");
                _logger.LogInformation("Host: {HostingInternalAddress} {HostExternalAddress}, Client: {ClientInternalAddress} {ClientExternalAddress}", waitPeer.InternalAddr, waitPeer.ExternalAddr, localEndPoint, remoteEndPoint);

                // host internal
                // host external
                // client internal
                // client external
                // request token
                _puncher.NatPunchModule.NatIntroduce(waitPeer.InternalAddr, waitPeer.ExternalAddr, localEndPoint, remoteEndPoint, token);

                // No longer waiting
                _waitingPeers.Remove(token);
            } 
            else 
            {
                // Only one peer has talked to the NAT server, store it and wait for the other peer
                _logger.LogInformation("Wait peer created: {InternalAddress} {ExternalAddress}", localEndPoint, remoteEndPoint);
                _waitingPeers[token] = new WaitPeer(localEndPoint, remoteEndPoint);
            }
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            // Ignore as we are the server
        }
    }
}