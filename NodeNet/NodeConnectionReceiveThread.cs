using System;
using System.Threading.Tasks;

namespace NodeNet
{
    internal class NodeConnectionReceiveThread
    {
        internal event Action OnFailure;
        internal event Action<Message> OnMessageReceived;
        internal event Action OnNetTimeout;
        private NodeClient client;
        DateTime lastRemoteContact;
        private Trace trace;

        internal NodeConnectionReceiveThread(NodeClient client)
        {
            this.client = client;
        }

        public NodeConnectionReceiveThread(NodeClient client, Trace trace) : this(client)
        {
            this.trace = trace;
        }

        bool IsActive { get; set; }
        internal void Start()
        {
            IsActive = true;
            lastRemoteContact = DateTime.Now;
            Task.Run(() => ThreadProc());
        }

        internal void Shutdown()
        {
            IsActive = false;
        }

        void ThreadProc()
        {
            while(IsActive)
            {
                try
                {
                    var message = client.Reader.ReceiveMessage();
                    lastRemoteContact = DateTime.Now; //TODO DST
                    if (message.Type == MessageType.KeepAlive)
                    {
                        trace.Emit(TraceEventId.KeepaliveReceived, client.RemoteNodeInfo.Name);
                        //consume keepalives
                        continue;
                    }
                    OnMessageReceived?.Invoke(message);
                }
                catch (Exception e)
                {
                    //the receive is designed to timeout periodically
                    if (ProtocolException.IsSocketTimeoutException(e))
                    {
                        //TODO only one side has to send keepalives
                        //if (client.IdleTimeoutPolicy.SendKeepaliveResponses)
                        {
                            if (DateTime.Now.Subtract(lastRemoteContact).TotalSeconds > 30) //client.IdleTimeoutPolicy.Interval)
                            {
                                //tracer.Failure(TraceEventId.IdleTimeoutTriggered);
                                OnFailure?.Invoke();
                            }
                        }
                        OnNetTimeout?.Invoke();
                        continue;
                    }
                    //TODO
                    //if (!client.IsConnected)
                    //{
                    //    server.Logger.Log("Connection closed remotely");
                    //    server.OnHandlerFault(this);
                    //    break;
                    //}
                    //tracer.Failure(TraceEventId.ClientMessageReceiveError, e.ToString());
                    OnFailure?.Invoke();
                    break;
                }
            }
        }
    }
}
