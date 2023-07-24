using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeNet
{
    //1 byte: message type
    //n bytes: message type specific data
    internal abstract class Message
    {
        internal MessageType Type { get; set; }
        public Message(MessageType type)
        {
            Type = type;
        }
    }

    internal enum MessageType
    {
        Request,
        Response,
        KeepAlive
    }

    //keepalive message
    //1 byte: 2
    internal class KeepAliveMessage : Message
    {
        public KeepAliveMessage() : base(MessageType.KeepAlive)
        {
        }
    }

    //request message
    //1 byte: 0
    //4 byte: request ID
    //1 byte: length of target name
    //        if this is 255 then the target is a 16 byte GUID
    //n bytes: target name or GUID
    //4 bytes: length of request data
    //n bytes: request data
    internal class RequestMessage : Message
    {
        public uint RequestId { get; set; }
        public string TargetNodeName { get; }
        public Guid TargetNodeId { get; }
        public byte[] RequestData { get; }

        public RequestMessage(uint requestId, string targetName, Guid targetGuid, byte[] requestData) : base(MessageType.Request)
        {
            RequestId = requestId;
            TargetNodeName = targetName;
            TargetNodeId = targetGuid;
            RequestData = requestData;
        }

        public override string ToString()
        {
            var command = Encoding.ASCII.GetString(RequestData);
            return $"rid={RequestId} to={TargetNodeName} cmd={command}";
        }
    }

    //response message
    //1 byte: 1
    //4 byte: request ID
    //4 bytes: length of response data
    //n bytes: response data
    internal class ResponseMessage : Message
    {
        public uint RequestId { get; set; }
        public byte[] ResponseData { get; }

        public ResponseMessage(uint requestId, byte[] responseData) : base(MessageType.Response)
        {
            this.RequestId = requestId;
            this.ResponseData = responseData;
        }

        public override string ToString()
        {
            var command = Encoding.ASCII.GetString(ResponseData);
            return $"rid={RequestId} cmd={command}";
        }
    }
}
