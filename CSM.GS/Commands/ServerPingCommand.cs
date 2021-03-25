using ProtoBuf;

namespace CSM.GS.Commands
{
    /// <summary>
    ///     This command is sent every x seconds to
    ///     confirm that the server is still running
    /// </summary>
    [ProtoContract]
    public class ServerPingCommand
    { }
}