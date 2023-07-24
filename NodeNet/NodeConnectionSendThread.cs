using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NodeNet
{
    internal class NodeConnectionSendThread
    {
        private NodeClient client;
        BlockingCollection<Message> sendQueue = new BlockingCollection<Message>();
        internal NodeConnectionSendThread(NodeClient client)
        {
            this.client = client;
            cancellationToken = cancellationSource.Token;
        }

        internal event Action OnFailure;
        bool IsActive { get; set; }
        internal void Start()
        {
            IsActive = true;
            Task.Run(() => ThreadProc());
        }

        internal void Shutdown()
        {
            IsActive = false;
            cancellationSource.Cancel();
        }

        DateTime nextKeepAlive;
        int SecondsUntilNextKeepalive()
        {
            if (!SendKeepAlives)
            {
                return -1; //infinite
            }
            return Math.Max(0, (int)nextKeepAlive.Subtract(DateTime.Now).TotalSeconds);
        }

        internal void Send(Message message)
        {
            sendQueue.Add(message);
        }

        bool SendKeepAlives
        {
            get
            {
                return KeepAliveInterval > 0;
            }
        }
        int KeepAliveInterval
        {
            get
            {
                return 20; // client.IdleTimeoutPolicy.Interval / 2 + 1;
            }
        }
        CancellationToken cancellationToken;
        CancellationTokenSource cancellationSource = new CancellationTokenSource();
        void ThreadProc()
        {
            if (SendKeepAlives)
            {
                nextKeepAlive = DateTime.Now.AddSeconds(KeepAliveInterval);
            }
            try
            {
                while (IsActive)
                {
                    Message message;
                    int waitTime = SecondsUntilNextKeepalive() * 1000;
                    var isMessageToSend = sendQueue.TryTake(out message, waitTime, cancellationToken);
                    if (isMessageToSend)
                    {
                        client.Writer.WriteMessage(message);
                    }

                    if (SendKeepAlives && SecondsUntilNextKeepalive() < 1)
                    {
                        sendQueue.Add(new KeepAliveMessage());
                        nextKeepAlive = DateTime.Now.AddSeconds(KeepAliveInterval);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //cancellationToken cancelled
            }
            catch (Exception e)
            {
                //tracer.Failure(TraceEventId.ClientMessageSendError, e.ToString());
                OnFailure?.Invoke();
            }
        }
    }
}
