using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NodeNet
{
    internal class NodeClient
    {
        internal event Action<RequestMessage, NodeClient> OnRequestReceived;
        //internal event Action<ResponseMessage, NodeConnectionClient> OnResponseReceived;
        internal event Action<NodeClient> OnShutdown;

        TcpClient tcpClient;
        ProtocolConnectionHandshake handshake = new ProtocolConnectionHandshake();
        internal ProtocolReader Reader { get; private set; }
        internal ProtocolWriter Writer { get; private set; }
        bool isOutgoing;
        internal NodeInfo RemoteNodeInfo { get; private set; } = new NodeInfo();

        NodeConnectionReceiveThread receiveThread;
        NodeConnectionSendThread sendThread;

        internal NodeClient(TcpClient tcpClient, bool isOutgoing)
        {
            this.tcpClient = tcpClient;
            this.isOutgoing = isOutgoing;
            Stream stream = tcpClient.GetStream();
            stream.ReadTimeout = 10000;
            stream.WriteTimeout = 10000;
            Reader = new ProtocolReader(stream);
            Writer = new ProtocolWriter(stream);
        }

        internal void Start()
        {
            receiveThread = new NodeConnectionReceiveThread(this);
            receiveThread.OnMessageReceived += ReceiveThread_OnMessageReceived;
            receiveThread.OnFailure += ReceiveThread_OnFailure;
            sendThread = new NodeConnectionSendThread(this);
            sendThread.OnFailure += SendThread_OnFailure;
            receiveThread.Start();
            sendThread.Start();
        }

        private void ReceiveThread_OnMessageReceived(Message message)
        {
            Trace.Instance.Emit(TraceEventId.OnMessageReceived, message);
            if (message.Type == MessageType.Request)
            {
                OnRequestReceived?.Invoke(message as RequestMessage, this);
            }
            else if (message.Type == MessageType.Response)
            {
                var responseMessage = message as ResponseMessage;
                lock (pendingResponses)
                {
                    PendingResponseRecord pendingResponse;
                    if (!pendingResponses.TryGetValue(responseMessage.RequestId, out pendingResponse))
                    {
                        //no matching request was sent, or it has already timed out. Ignore.
                        Trace.Instance.Emit(TraceEventId.MismatchingResponse, responseMessage.RequestId);
                        return;
                    }
                    pendingResponses.Remove(responseMessage.RequestId);
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
        internal void Shutdown()
        {
            Trace.Instance.Emit(TraceEventId.ClientShutdown, RemoteNodeInfo.Name);
            receiveThread?.Shutdown();
            sendThread?.Shutdown();
            OnShutdown?.Invoke(this);
        }

        internal bool DoHandshake(Node thisNode)
        {
            bool success;
            if (isOutgoing)
                success = handshake.DoHandshakeAsClient(this, thisNode);
            else
                success = handshake.DoHandshakeAsServer(this, thisNode);
            return success;
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
        internal async Task<ResponseMessage> SendAsync(RequestMessage message, int responseTimeoutMs=10000)
        {
            if (responseTimeoutMs > 0)
            {
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
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    tcs.SetException(new TimeoutException());
                    return tcs.Task.Result;
                }
                else
                {
                    return tcs.Task.Result;
                }
            }
            else
            {
                sendThread.Send(message);
            }
            return null;
        }

        internal void SendResponse(ResponseMessage message)
        {
            Trace.Instance.Emit(TraceEventId.SendResponse, message);
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
