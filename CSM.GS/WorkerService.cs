using LiteNetLib;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CSM.GS
{
    /// <summary>
    ///     This background service a UDP server which matches
    ///     servers and clients up via NAT hole punching.
    ///
    ///     TODO:
    ///         - What happens to servers that the GS thinks are gone,
    ///           but still exist (removed from list, but receiving ping events)
    ///         - If the GS crashes, gracefully handling re-setup of all the servers
    ///         - Let clients connect using a token instead of an IP address
    ///         - Gracefully handle exceptions and restart the service (prob through docker)
    ///         - Configurable ports + tick rate
    /// </summary>
    public class WorkerService : BackgroundService, INatPunchListener
    {
        // Constants
        private const int ServerPort = 4240;

        private const int ServerTick = 10;

        private static readonly TimeSpan KickTime = new(0, 0, 10);

        private NetManager _puncher;
        private readonly ILogger _logger;

        private readonly Dictionary<IPAddress, Server> _gameServers = new();
        private readonly List<IPAddress> _serversToRemove = new();

        public WorkerService(ILogger<WorkerService> logger)
        {
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var netListener = new EventBasedNetListener();

            // Here we update the last contact time in the list of internal servers
            netListener.NetworkReceiveUnconnectedEvent += (point, _, type) =>
            {
                if (type != UnconnectedMessageType.BasicMessage)
                    return;

                // If this server exists, refresh it
                if (_gameServers.TryGetValue(point.Address, out var server))
                {
                    server.Refresh();
                }
            };

            _puncher = new NetManager(netListener);

            _puncher.Start(ServerPort);
            _puncher.NatPunchEnabled = true;
            _puncher.UnconnectedMessagesEnabled = true;
            _puncher.NatPunchModule.Init(this);

            _logger.LogInformation("Starting NAT Relay Server...");

            // Loop
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;

                _puncher.NatPunchModule.PollEvents();
                _puncher.PollEvents();

                // Check old servers, if a server has been waiting for longer than
                // "KickTime", they need to be removed from the server list
                foreach (var (ip, server) in _gameServers)
                {
                    if (now - server.LastPing > KickTime)
                    {
                        _serversToRemove.Add(ip);
                    }
                }

                // Now actually remove servers that are due for removal
                for (var i = 0; i <= _serversToRemove.Count - 1; i++)
                {
                    _logger.LogInformation("[{ExternalAddress}] Server has disconnected, removing from internal dictionary...", _serversToRemove[i]);
                    _gameServers.Remove(_serversToRemove[i]);
                }

                _serversToRemove.Clear();

                Thread.Sleep(ServerTick);
            }

            _logger.LogInformation("Stopping NAT Relay Server...");

            _puncher.Stop();
            return Task.CompletedTask;
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            // Incoming formats
            // Server: server_{token}
            // Client: client_{server_ip:port}

            // This is a server connecting
            if (token.StartsWith("server_"))
            {
                if (_gameServers.ContainsKey(remoteEndPoint.Address))
                {
                    _logger.LogInformation("[{ExternalAddress}] Server is already registered, refreshing...", remoteEndPoint);
                }
                else
                {
                    _logger.LogInformation("[{ExternalAddress}] Registered Server: Internal Address={InternalAddress} Token={Token}", remoteEndPoint, localEndPoint, token);
                    _gameServers[remoteEndPoint.Address] = new Server(localEndPoint, remoteEndPoint, token.Split('_')[1]);
                }
            }
            else // This is a client connecting
            {
                var serverIp = IPAddress.Parse(token.Split('_')[1]);
                if (_gameServers.TryGetValue(serverIp, out var server))
                {
                    // At this point, we have access to the client and server, we can now introduce them
                    _logger.LogInformation("Server found, sending introduction...");
                    _logger.LogInformation("Host: {HostingInternalAddress} {HostExternalAddress}, Client: {ClientInternalAddress} {ClientExternalAddress}", server.InternalAddress, server.ExternalAddress, localEndPoint, remoteEndPoint);

                    // host internal
                    // host external
                    // client internal
                    // client external
                    // request token
                    _puncher.NatPunchModule.NatIntroduce(server.InternalAddress, server.ExternalAddress, localEndPoint, remoteEndPoint, token);
                }
                else
                {
                    _logger.LogInformation("Server not found, ignoring...");
                }
            }
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            // Ignore as we are the server
        }
    }
}