using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NodeNet
{
    /// <summary>
    /// Manages connection to a another node
    /// </summary>
    internal class NodeClient
    {
        internal NodeInfo RemoteNodeInfo { get; private set; } = new NodeInfo();
        internal event Action<RequestMessage, NodeClient> OnRequestReceived;
        internal event Action<NodeClient> OnShutdown;
        internal ProtocolReader Reader { get; private set; }
        internal ProtocolWriter Writer { get; private set; }
        TcpClient tcpClient;
        NodeConnectionReceiveThread receiveThread;
        NodeConnectionSendThread sendThread;

        /// <summary>
        /// Instantiate with a connected TCP client and whether we are connecting to another node or being connected to
        /// </summary>
        internal NodeClient(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
            Stream stream = tcpClient.GetStream();
            stream.ReadTimeout = 10000;
            stream.WriteTimeout = 10000;
            Reader = new ProtocolReader(stream);
            Writer = new ProtocolWriter(stream);
        }

        public NodeClient(TcpClient tcpClient, Trace trace) : this(tcpClient)
        {
            this.trace = trace;
            RemoteNodeInfo.Endpoint = tcpClient.Client.RemoteEndPoint;
        }

        internal void Start()
        {
            //for now there's a separate receive and send thread for each client
            //which drive the protocol reader and writer
            receiveThread = new NodeConnectionReceiveThread(this, trace);
            receiveThread.OnMessageReceived += ReceiveThread_OnMessageReceived;
            receiveThread.OnFailure += ReceiveThread_OnFailure;
            sendThread = new NodeConnectionSendThread(this);
            sendThread.OnFailure += SendThread_OnFailure;
            receiveThread.Start();
            sendThread.Start();
        }

        /// <summary>
        /// Inbound message received
        /// </summary>
        private void ReceiveThread_OnMessageReceived(Message message)
        {
            trace.Emit(TraceEventId.OnMessageReceived, message);
            if (message.Type == MessageType.Request)
            {
                //inbound requests are handled by the node
                OnRequestReceived?.Invoke(message as RequestMessage, this);
            }
            else if (message.Type == MessageType.Response)
            {
                //inbound responses must be matched with a previously sent request
                var responseMessage = message as ResponseMessage;
                lock (pendingResponses)
                {
                    PendingResponseRecord pendingResponse;
                    if (!pendingResponses.TryGetValue(responseMessage.RequestId, out pendingResponse))
                    {
                        //no matching request was sent, or it has already timed out. Ignore.
                        trace.Emit(TraceEventId.MismatchingResponse, responseMessage.RequestId);
                        return;
                    }
                    pendingResponses.Remove(responseMessage.RequestId);
                    
                    //forward the response on
                    //replace request ID with the original request ID set by the request sender
                    responseMessage.RequestId = pendingResponse.OriginalRequestId;
                    Task.Run(() =>
                    {
                        pendingResponse.TaskCompletionSource.SetResult(responseMessage);
                    });
                }
            }
        }

        private void ReceiveThread_OnFailure()
        {
            Shutdown();
        }
        private void SendThread_OnFailure()
        {
            Shutdown();
        }

        /// <summary>
        /// Shuts down the connection to the remote node
        /// </summary>
        internal void Shutdown()
        {
            trace.Emit(TraceEventId.ClientShutdown, RemoteNodeInfo.Name);
            receiveThread?.Shutdown();
            sendThread?.Shutdown();
            OnShutdown?.Invoke(this);
        }

        class PendingResponseRecord
        {
            internal uint RequestId;
            internal uint OriginalRequestId;
            internal TaskCompletionSource<ResponseMessage> TaskCompletionSource;

            public PendingResponseRecord(uint requestId, uint originalRequestId, TaskCompletionSource<ResponseMessage> taskCompletionSource)
            {
                OriginalRequestId = originalRequestId;
                RequestId = requestId;
                TaskCompletionSource = taskCompletionSource;
            }
        }

        Dictionary<uint, PendingResponseRecord> pendingResponses = new Dictionary<uint, PendingResponseRecord>();
        uint nextRequestId = 1;
        private Trace trace;

        /// <summary>
        /// Sends an outbound request to the connected node
        /// </summary>
        internal async Task<ResponseMessage> SendAsync(RequestMessage message, int responseTimeoutMs=10000)
        {
            bool expectResponse = responseTimeoutMs > 0;
            if (!expectResponse)
            {
                sendThread.Send(message);
                return null;
            }

            //create a pending response entry
            //a response is expected within the timeout window or a timeout response will be generated and returned
            var timeoutTask = Task.Delay(responseTimeoutMs);
            var tcs = new TaskCompletionSource<ResponseMessage>();
            lock (pendingResponses)
            {
                var pendingRecord = new PendingResponseRecord(nextRequestId, message.RequestId, tcs);
                message.RequestId = nextRequestId;
                nextRequestId++;
                pendingResponses.Add(message.RequestId, pendingRecord);
            }
            sendThread.Send(message);
            //which ever completes first
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            if (completedTask == timeoutTask)
            {
                lock (pendingResponses)
                {
                    pendingResponses.Remove(nextRequestId);
                }
                tcs.SetException(new TimeoutException());
                return tcs.Task.Result;
            }
            else
            {
                return tcs.Task.Result;
            }
        }

        /// <summary>
        /// Sends an outbound response to the remote node
        /// </summary>
        internal void SendResponse(ResponseMessage message)
        {
            trace.Emit(TraceEventId.SendResponse, message);
            sendThread.Send(message);
        }

        internal bool MatchesRemoteNode(Guid nodeId, string nodeName)
        {
            if (nodeId == RemoteNodeInfo.Id)
                return true;
            if (nodeId != Guid.Empty)
                return false;
            if (nodeName == RemoteNodeInfo.Name)
                return true;
            return false;
        }
    }
}
