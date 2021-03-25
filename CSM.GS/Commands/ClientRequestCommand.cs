using ProtoBuf;

namespace CSM.GS.Commands
{
    /// <summary>
    ///     Command sent from the client when they are
    ///     requesting to join a server
    /// </summary>
    [ProtoContract]
    public class ClientRequestCommand
    {
        /// <summary>
        ///     Either 0 for IP or 1 for Token
        /// </summary>
        [ProtoMember(1)]
        public short Type { get; set; }
        
        /// <summary>
        ///     <para>If Type=0: IP</para>
        ///     <para>If Type=1: Token</para>
        /// </summary>
        [ProtoMember(2)]
        public string ServerId { get; set; }
    }
}