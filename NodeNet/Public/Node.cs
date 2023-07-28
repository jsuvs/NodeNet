using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NodeNet
{
    /// <summary>
    /// Provides methods to start a node, connect to other nodes and send and receive messages
    /// </summary>
    public class Node
    {
        /// <summary>
        /// Request data delegate
        /// </summary>
        /// <param name="requestData">request data</param>
        /// <returns>response data</returns>
        public delegate byte[] ReceiveRequestDelegate(byte[] requestData);

        /// <summary>
        /// Raised when the node receives data from connected node
        /// </summary>
        public event ReceiveRequestDelegate OnReceiveRequest;

        public delegate void TraceEventDelegate(TraceEventId eventId, string arguments);

        /// <summary>
        /// Raised on various internal events occuring
        /// </summary>
        public event TraceEventDelegate OnTraceEvent;

        internal NodeInfo Info { get; set; } = new NodeInfo();
        BlockingCollection<NodeClient> clients = new BlockingCollection<NodeClient>();
        Connector connector;
        
        /// <summary>
        /// Creates a new node with the given name
        /// </summary>
        public Node(string name)
        {
            Info.Id = Guid.NewGuid();
            Info.Capabilities = NodeCapabilities.None;
            Info.Name = name;
            connector = new Connector();
            connector.OnClientConnect += Connector_OnClientConnect;
            Trace.Instance.OnEvent += Instance_OnEvent;
        }

        /// <summary>
        /// Connect this node to a remote node
        /// </summary>
        public void Connect(string host, int port)
        {
            Trace.Instance.Emit(TraceEventId.Connect, host, port);
            connector.Connect(host, port);
        }

        /// <summary>
        /// Start listening on a port and accept connecting remote nodes
        /// </summary>
        public void StartListener(int port)
        {
            Trace.Instance.Emit(TraceEventId.StartListener, port);
            connector.StartListener(port);
        }

        /// <summary>
        /// Send data to a remote node
        /// </summary>
        /// <param name="requestData">request data</param>
        /// <param name="destination">optional, name of node to send data to</param>
        /// <returns>A response</returns>
        public Response Send(byte[] requestData, string destination = null)
        {
            return SendAsync(requestData, destination).Result;
        }

        uint nextRequestId = 1;

        /// <summary>
        /// Asynchronously send data to a remote node
        /// </summary>
        /// <param name="requestData">request data</param>
        /// <param name="destination">optional, name of node to send data to</param>
        /// <returns>A response</returns>
        public async Task<Response> SendAsync(byte[] data, string destination = null)
        {
            Trace.Instance.Emit(TraceEventId.SendAsync, data, destination);
            //TODO: for now if there is only one connected node then dont bother resolving the destination
            var handler = clients.FirstOrDefault();
            if (clients.Count > 1)
            {
                //if connected to more than one node then decide the node to send it to based on the destination
                handler = Resolve(Guid.Empty, destination);
                if (handler == null)
                {
                    throw new Exception("destination not found");
                }
            }

            //form the request and assign a unique request ID
            var message = new RequestMessage(nextRequestId, destination, Guid.Empty, data);
            nextRequestId++;
            try
            {
                var response = await handler.SendAsync(message);
                Trace.Instance.Emit(TraceEventId.ResponseReceived, response.RequestResult);
                if (response.RequestResult == RequestResult.Success)
                    return new Response(ResponseStatus.Success, response.ResponseData);
                else if (response.RequestResult == RequestResult.Timeout)
                    return new Response(ResponseStatus.Timeout, null);
                else if (response.RequestResult == RequestResult.ResolveError)
                    return new Response(ResponseStatus.ResolveFailure, null);
                else
                    return new Response(ResponseStatus.UnknownError, null);
            }
            catch (Exception e)
            {
                if (e.InnerException is TimeoutException)
                {
                    Trace.Instance.Emit(TraceEventId.Timeout, e);
                    return new Response(ResponseStatus.Timeout, null);
                }
                Trace.Instance.Emit(TraceEventId.SendError, e);
                return new Response(ResponseStatus.UnknownError, null);
            }
        }

        /// <summary>
        /// called when either this node connects to a remote node or a remote node connects to us
        /// isOutgoing identifies the direction
        /// </summary>
        private void Connector_OnClientConnect(TcpClient tcpClient, bool isOutgoing)
        {
            Trace.Instance.Emit(TraceEventId.OnClientConnect, tcpClient.Client.RemoteEndPoint);
            var client = new NodeClient(tcpClient);
            bool success = connector.DoHandshake(client, this, isOutgoing);
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
            //this method is called from the receiving node client thread. Nothing in here must block
            //TODO refactor
            Trace.Instance.Emit(TraceEventId.OnRequestReceived, sourceClient.RemoteNodeInfo.Name, message.ToString());
            //if the request is for this node then it should be handled by the application
            if (IsSelf(message.TargetNodeId, message.TargetNodeName))
            {
                var requestData = message.RequestData;
                //run event on different thread so the receive thread is not blocked
                await Task.Run(() =>
                {
                    byte[] response = OnReceiveRequest?.Invoke(requestData);
                    if (response != null)
                    {
                        var responseMessage = new ResponseMessage(message.RequestId, RequestResult.Success, response);
                        sourceClient.SendResponse(responseMessage);
                    }
                });
            }
            else
            { 
                //request is not for this node
                //determine which connected node to forward the request to
                var client = Resolve(message.TargetNodeId, message.TargetNodeName);
                if (client == null)
                {
                    sourceClient.SendResponse(new ResponseMessage(message.RequestId, RequestResult.ResolveError, new byte[0]));
                    return;
                }

                //TODO check - I suspect the message requestId gets modified so store the original here
                var requestId = message.RequestId;
                try
                {
                    //forward the request and later forward back the response
                    var response = await client.SendAsync(message);
                    sourceClient.SendResponse(response);
                }
                catch (Exception e)
                {
                    if (e.InnerException is TimeoutException)
                    {
                        sourceClient.SendResponse(new ResponseMessage(requestId, RequestResult.Timeout, new byte[0]));
                    }
                    Trace.Instance.Emit(TraceEventId.ForwardError, e);
                }
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
}

