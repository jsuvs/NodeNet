using System;
using System.Text;

namespace NodeNet
{
    internal class ProtocolConnectionHandshake
    {
        internal bool DoHandshakeAsClient(NodeClient client, Node thisNode)
        {
            try
            {
                client.Writer.WriteData16(Encoding.ASCII.GetBytes("device"));
                var data = client.Reader.ReadData16();
                if (Encoding.ASCII.GetString(data) != "server")
                    return false;
                client.Writer.WriteBytes(new byte[] { (byte)thisNode.Info.Capabilities });
                //send 16 byte device ID
                client.Writer.WriteBytes(thisNode.Info.Id.ToByteArray());
                client.Writer.WriteData8(Encoding.ASCII.GetBytes(thisNode.Info.Name));
                client.RemoteNodeInfo.Capabilities = (NodeCapabilities)client.Reader.ReadBytes(1)[0];
                client.RemoteNodeInfo.Id = new Guid(client.Reader.ReadBytes(16));
                var serverName = Encoding.ASCII.GetString(client.Reader.ReadData8());
                //trace.Detail(TraceEventId.HandshakeAsClientReceiveRemoteName, serverName);
                client.RemoteNodeInfo.Name = serverName;
                //idle timeout interval in seconds. 0 = off
                //the hub will send keep alive packets frequently enough
                ushort idleTimeoutInterval = client.Reader.ReadUInt16();
                //whether the device should reply to keepalives
                bool replyToKeepalives = client.Reader.ReadBytes(1)[0] > 0;
                //client.SetIdleTimeoutPolicy(idleTimeoutInterval, replyToKeepalives);
                client.Writer.WriteBytes(Encoding.ASCII.GetBytes("ok"));
            }
            catch (Exception)
            {
                //trace.Failure(TraceEventId.DeviceHandshakeAsClientFailure);
                return false;
            }
            return true;
        }

        internal bool DoHandshakeAsServer(NodeClient client, Node thisNode)
        {
            try
            {
                //TODO reader.settimeout
                var data = client.Reader.ReadData16();
                if (data == null)
                    return false;
                if (Encoding.ASCII.GetString(data) != "device")
                    return false;
                //TODO timeout catch IOException on receive?
                //write "server"
                client.Writer.WriteData16(Encoding.ASCII.GetBytes("server"));
                client.RemoteNodeInfo.Capabilities = (NodeCapabilities)client.Reader.ReadBytes(1)[0];
                client.RemoteNodeInfo.Id = new Guid(client.Reader.ReadBytes(16));
                var clientDeviceName = Encoding.ASCII.GetString(client.Reader.ReadData8());
                client.RemoteNodeInfo.Name = clientDeviceName;
                client.Writer.WriteBytes(new byte[] { (byte)thisNode.Info.Capabilities });
                //send 16 byte device ID
                client.Writer.WriteBytes(thisNode.Info.Id.ToByteArray());
                client.Writer.WriteData8(Encoding.ASCII.GetBytes(thisNode.Info.Name));
                //idle timeout interval in seconds. 0 = off
                //the hub will send keep alive packets frequently enough
                client.Writer.WriteUInt16((ushort)30); // client.IdleTimeoutPolicy.Interval);
                //whether the device should reply to keepalives
                client.Writer.WriteByte(1);
                data = client.Reader.ReadBytes(2);
                if (data == null)
                    return false;
                if (Encoding.ASCII.GetString(data) != "ok")
                    return false;
            }
            catch (Exception e)
            {
                //TODO
                //IOException with inner exception SocketException, SocketErrorCode::TimedOut indicates timeout
                //trace.Failure(TraceEventId.DeviceHandshakeAsServerFailure, e.ToString());
                return false;
            }
            return true;
        }
    }
}
