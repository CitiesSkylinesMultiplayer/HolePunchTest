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
        private static readonly TimeSpan KickTime = TimeSpan.FromSeconds(15);

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
            EventBasedNetListener netListener = new();

            // Here we update the last contact time in the list of internal servers
            netListener.NetworkReceiveUnconnectedEvent += (point, _, type) =>
            {
                if (type != UnconnectedMessageType.BasicMessage)
                    return;

                // If this server exists, refresh it
                if (_gameServers.TryGetValue(point.Address, out Server server))
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
                DateTime now = DateTime.Now;

                _puncher.NatPunchModule.PollEvents();
                _puncher.PollEvents();

                // Check old servers, if a server has been waiting for longer than
                // "KickTime", they need to be removed from the server list
                foreach ((IPAddress ip, Server server) in _gameServers)
                {
                    if (now - server.LastPing > KickTime)
                    {
                        _serversToRemove.Add(ip);
                    }
                }

                // Now actually remove servers that are due for removal
                foreach (IPAddress ip in _serversToRemove)
                {
                    _logger.LogInformation("[{ExternalAddress}] Server has disconnected, removing from internal dictionary...", Anonymize(ip));
                    _gameServers.Remove(ip);
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

            string[] tokenParts = token.Split('_');
            if (tokenParts.Length != 2)
            {
                return;
            }

            // This is a server connecting
            if (tokenParts[0] == "server")
            {
                if (_gameServers.ContainsKey(remoteEndPoint.Address))
                {
                    _logger.LogInformation("[{ExternalAddress}] Server is already registered, refreshing...", Anonymize(remoteEndPoint));
                }
                else
                {
                    _logger.LogInformation("[{ExternalAddress}] Registered Server: Internal Address={InternalAddress} Token={Token}", Anonymize(remoteEndPoint), Anonymize(localEndPoint), tokenParts);
                }
                // Always create new server entry, so that port numbers and the token are updated
                _gameServers[remoteEndPoint.Address] = new Server(localEndPoint, remoteEndPoint, tokenParts[1]);
            }
            else if (tokenParts[0] == "client") // This is a client connecting
            {
                IPAddress serverIp;
                try
                {
                    serverIp = IPAddress.Parse(tokenParts[1]);
                }
                catch (FormatException)
                {
                    return;
                }

                if (_gameServers.TryGetValue(serverIp, out Server server))
                {
                    // At this point, we have access to the client and server, we can now introduce them
                    _logger.LogInformation("Introduction -> Host: {HostingInternalAddress} {HostExternalAddress}, Client: {ClientInternalAddress} {ClientExternalAddress}", Anonymize(server.InternalAddress), Anonymize(server.ExternalAddress), Anonymize(localEndPoint), Anonymize(remoteEndPoint));

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

        private static string Anonymize(IPEndPoint endpoint)
        {
            return Anonymize(endpoint.Address) + ":" + endpoint.Port;
        }

        private static string Anonymize(IPAddress address)
        {
            string[] parts = address.ToString().Split('.');
            if (parts.Length == 4)
            {
                return parts[0] + '.' + parts[1] + ".x.x";
            }
            else
            {
                return address.ToString();
            }
        }
    }
}
