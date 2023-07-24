using System;
using static NodeNet.Node;

namespace NodeNet
{
    internal class Trace
    {
        static Trace()
        {
            Instance = new Trace();
        }
        private Trace() { }
        internal static Trace Instance { get; private set; }

        internal event TraceEventDelegate OnEvent;

        internal void Emit(TraceEventId e, params object[] args)
        {
            string argText = string.Join(", ", args);
            OnEvent?.Invoke(e, argText);
        }
    }

    public enum TraceEventId
    {
        Connect,
        StartListener,
        SendAsync,
        OnClientConnect,
        HandshakeFail,
        HandshakeSuccess,
        OnRequestReceived,
        ListenerStarted,
        ListenerAccept,
        OnMessageReceived,
        MismatchingResponse,
        ClientShutdown,
        SendResponse,
        KeepaliveReceived,
        SendError
    }
}
