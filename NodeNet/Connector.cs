using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NodeNet
{
    internal class Connector
    {
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
                Trace.Instance.Emit(TraceEventId.ListenerStarted, port);
                while (true)
                {
                    var client = listener.AcceptTcpClient();
                    Trace.Instance.Emit(TraceEventId.ListenerAccept, Convert.ToString(client.Client.RemoteEndPoint));
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

        internal delegate void OnClientConnectDelegate(TcpClient tcpClient, bool isOutgoing);
        internal event OnClientConnectDelegate OnClientConnect;
    }
}
