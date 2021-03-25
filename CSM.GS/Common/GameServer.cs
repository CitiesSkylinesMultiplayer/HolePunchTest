using System;
using System.Net;

namespace CSM.GS.Common
{
    /// <summary>
    ///     Represents a game server connected to the current CSM GS.
    ///     As long as the game server is running, this object will stay
    ///     in memory.
    /// </summary>
    public class GameServer
    {
        /// <summary>
        ///     A unique token identifying this server (so the public IP address does
        ///     not need to be displayed)
        /// </summary>
        public string Token { get; }
        
        /// <summary>
        ///     The internal IP address (behind the NAT) of the server, also known
        ///     as the local or private IP.
        /// </summary>
        public IPEndPoint InternalAddress { get; }

        /// <summary>
        ///     The public IP address (in front of the NAT) of the server, also
        ///     known as the public ip address (what the client will connect to)
        /// </summary>
        public IPEndPoint ExternalAddress { get; }
        
        /// <summary>
        ///     Time of the last ping of the server, used to determine if the server
        ///     is still running.
        /// </summary>
        public DateTime LastPing { get; private set; }

        /// <summary>
        ///     If this is a dedicated server, always false, here for potential
        ///     future proofing.
        /// </summary>
        public bool Dedicated => false;

        public GameServer(IPEndPoint internalAddress, IPEndPoint externalAddress)
        {
            Refresh();

            Token = Helpers.GenerateRandomToken();
            InternalAddress = internalAddress;
            ExternalAddress = externalAddress;
        }
        
        public void Refresh()
        {
            LastPing = DateTime.Now;
        }
    }
}