using System;
using System.IO;
using System.Net.Sockets;

namespace NodeNet
{
    public class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }

        public static bool IsSocketTimeoutException(Exception e)
        {
            if (!(e is IOException))
                return false;
            var socketException = e.InnerException as SocketException;
            if (socketException == null)
                return false;
            return socketException.SocketErrorCode == SocketError.TimedOut;
        }
    }
}
