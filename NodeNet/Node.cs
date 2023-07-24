using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NodeNet
{
    public class Node
    {
        public event Func<string, string> OnReceiveRequest;
        public delegate void TraceEventDelegate(TraceEventId eventId, string arguments);
        public event TraceEventDelegate OnTraceEvent;
        internal NodeInfo Info { get; set; } = new NodeInfo();
        BlockingCollection<NodeClient> clients = new BlockingCollection<NodeClient>();
        Connector connector;
       
        public Node(string name)
        {
            Info.Id = Guid.NewGuid();
            Info.Capabilities = NodeCapabilities.None;
            Info.Name = name;
            connector = new Connector();
            connector.OnClientConnect += Connector_OnClientConnect;
            Trace.Instance.OnEvent += Instance_OnEvent;
        }

        public void Connect(string host, int port)
        {
            Trace.Instance.Emit(TraceEventId.Connect, host, port);
            connector.Connect(host, port);
        }

        public void StartListener(int port)
        {
            Trace.Instance.Emit(TraceEventId.StartListener, port);
            connector.StartListener(port);
        }

        public string Send(string command, string destination = null)
        {
            return SendAsync(command, destination).Result;
        }

        uint nextRequestId = 1;
        public async Task<string> SendAsync(string command, string destination = null)
        {
            Trace.Instance.Emit(TraceEventId.SendAsync, command, destination);
            var handler = clients.FirstOrDefault();
            if (clients.Count > 1)
            {
                Resolve(Guid.Empty, destination);
                if (handler == null)
                {
                    throw new Exception("destination not found");
                }
            }
            var data = Encoding.ASCII.GetBytes(command);
            var message = new RequestMessage(nextRequestId, destination, Guid.Empty, data);
            nextRequestId++;
            try
            {
                var response = await handler.SendAsync(message);
                return Encoding.ASCII.GetString(response.ResponseData);
            }
            catch (Exception e)
            {
                Trace.Instance.Emit(TraceEventId.SendError, e);
                return null;
            }
        }

        private void Connector_OnClientConnect(TcpClient tcpClient, bool isOutgoing)
        {
            Trace.Instance.Emit(TraceEventId.OnClientConnect, tcpClient.Client.RemoteEndPoint);
            var client = new NodeClient(tcpClient, isOutgoing);
            bool success = client.DoHandshake(this);
            if (!success)
            {
                Trace.Instance.Emit(TraceEventId.HandshakeFail);
                //TODO
                return;
            }
            Trace.Instance.Emit(TraceEventId.HandshakeSuccess);
            client.OnRequestReceived += Client_OnRequestReceived;
            client.OnShutdown += Client_OnShutdown;
            clients.Add(client);
            client.Start();
        }

        private async void Client_OnRequestReceived(RequestMessage message, NodeClient sourceClient)
        {
            //this method is called from the receiving node handler. Nothing in here must block
            Trace.Instance.Emit(TraceEventId.OnRequestReceived, sourceClient.RemoteNodeInfo.Name, message.ToString());
            if (IsSelf(message.TargetNodeId, message.TargetNodeName))
            {
                var command = Encoding.ASCII.GetString(message.RequestData);
                //run event on different thread so the receive thread is not blocked
                await Task.Run(() =>
                {
                    string response = OnReceiveRequest?.Invoke(command);
                    if (response != null)
                    {
                        var responseMessage = new ResponseMessage(message.RequestId, Encoding.ASCII.GetBytes(response));
                        sourceClient.SendResponse(responseMessage);
                    }
                });
            }
            else
            { 
                var client = Resolve(message.TargetNodeId, message.TargetNodeName);
                var response = await client.SendAsync(message);
                sourceClient.SendResponse(response);
            }
        }

        bool IsSelf(Guid nodeId, string nodeName)
        {
            if (nodeId == Info.Id)
                return true;
            if (nodeId != Guid.Empty)
                return false;
            if (nodeName == null)
                return true;
            if (nodeName == Info.Name)
                return true;
            return false;
        }

        NodeClient Resolve(Guid nodeId, string nodeName)
        {
            lock (clients)
            {
                return clients.FirstOrDefault(h => h.MatchesRemoteNode(nodeId, nodeName));
            }
        }

        private void Client_OnShutdown(NodeClient client)
        {
            //TODO
        }

        internal void AddClient(NodeClient client)
        {
            lock (clients)
            {
                clients.Add(client);
            }
        }

        private void Instance_OnEvent(TraceEventId e, string args)
        {
            OnTraceEvent?.Invoke(e, args);
        }
    }

    internal class NodeInfo
    {
        internal Guid Id { get; set; }
        internal string Name { get; set; }
        internal NodeCapabilities Capabilities { get; set; }
    }
}
