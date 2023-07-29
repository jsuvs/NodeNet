using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NodeNet
{
    /// <summary>
    /// Acts to collapse the two cases 'listener accept' and 'connect as client' to a single 'new TCP client' event
    /// </summary>
    internal class Connector
    {
        internal delegate void OnClientConnectDelegate(TcpClient tcpClient, bool isOutgoing);
        internal event OnClientConnectDelegate OnClientConnect;
        ProtocolConnectionHandshake handshake;
        private Trace trace;

        public Connector(Trace trace)
        {
            this.trace = trace;
            handshake = new ProtocolConnectionHandshake(trace);
        }

        public void Connect(string host, int port)
        {
            var tcpClient = new TcpClient(host, port);
            //TODO TLS
            OnClientConnect?.Invoke(tcpClient, true);
        }

        public void StartListener(int port)
        {
            Task.Run(() => ListenerThread(port));
        }

        void ListenerThread(int port)
        {
            try
            {
                var listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
                listener.Start(10);
                trace.Emit(TraceEventId.ListenerStarted, port);
                while (true)
                {
                    var client = listener.AcceptTcpClient();
                    trace.Emit(TraceEventId.ListenerAccept, Convert.ToString(client.Client.RemoteEndPoint));
                    OnAccept(client);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
            }
        }

        void OnAccept(TcpClient tcpClient)
        {
            //TODO TLS
            OnClientConnect?.Invoke(tcpClient, false);
        }

        internal bool DoHandshake(NodeClient nodeClient, Node node, bool isClient)
        {
            bool success;
            if (isClient)
                success = handshake.DoHandshakeAsClient(nodeClient, node);
            else
                success = handshake.DoHandshakeAsServer(nodeClient, node);
            return success;
        }
    }
}
