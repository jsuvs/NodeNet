using System;
using static NodeNet.Node;

namespace NodeNet
{
    internal class Trace
    {

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
        SendError,
        ForwardError,
        Timeout,
        ResponseReceived,
        HandshakeAsServerError,
        ForwardMessage,
        ForwardResponse,
        RemoveClient
    }
}
