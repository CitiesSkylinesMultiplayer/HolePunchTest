using System;
using System.Net;

namespace Server
{
    public class WaitPeer
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