using System;
using System.Diagnostics;
using System.IO;

namespace NodeNet
{
    internal class ProtocolReader
    {
        public const int MAX_SIMPLE_REQUEST_DATA_LENGTH = 18192;
        BinaryReader reader;
        internal ProtocolReader(Stream stream)
        {
            reader = new BinaryReader(stream);
        }

        internal Message ReceiveMessage()
        {
            MessageType messageType = ReadMessageType();
            switch (messageType)
            {
                case MessageType.Request:
                    return ReceiveRequestMessage();
                case MessageType.Response:
                    return ReceiveResponseMessage();
                case MessageType.KeepAlive:
                    return new KeepAliveMessage();
                default:
                    throw new ProtocolException($"Unrecognised message type {messageType}");
            }
        }

        RequestMessage ReceiveRequestMessage()
        {
            uint requestId = reader.ReadUInt32();
            byte targetNameLength = reader.ReadByte();
            string targetName = null;
            Guid targetGuid = Guid.Empty;
            if (targetNameLength == 255)
            {
                targetGuid = new Guid(reader.ReadBytes(16));
            }
            else if (targetNameLength > 0)
            {
                targetName = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(targetNameLength));
            }

            uint requestDataLength = reader.ReadUInt32();
            //todo protect against attack
            var requestData = reader.ReadBytes((int)requestDataLength);
            var request = new RequestMessage(requestId, targetName, targetGuid, requestData);
            return request;
        }

        ResponseMessage ReceiveResponseMessage()
        {
            uint requestId = reader.ReadUInt32();
            var result = (RequestResult)reader.ReadByte();
            uint responseDataLength = reader.ReadUInt32();
            //todo protect against attack
            var responseData = reader.ReadBytes((int)responseDataLength);
            var response = new ResponseMessage(requestId, result, responseData);
            return response;
        }

        MessageType ReadMessageType()
        {
            ushort type = reader.ReadByte();
            return (MessageType)type;
        }

        internal byte[] ReadBytes(int numBytes)
        {
            return reader.ReadBytes(numBytes);
        }

        public byte[] ReadData16()
        {
            ushort dataLength = reader.ReadUInt16();
            if (dataLength > MAX_SIMPLE_REQUEST_DATA_LENGTH)
            {
                Debug.WriteLine($"Received length {dataLength} exceeds MAX_RECV_PACKET_SIZE {MAX_SIMPLE_REQUEST_DATA_LENGTH}");
                throw new ProtocolException($"Message length {dataLength} exceeds MAX_SIMPLE_REQUEST_DATA_LENGTH");
            }

            var data = reader.ReadBytes(dataLength);
            return data;
        }

        public byte[] ReadData8()
        {
            ushort dataLength = reader.ReadByte();
            var data = reader.ReadBytes(dataLength);
            return data;
        }

        internal ushort ReadUInt16()
        {
            return reader.ReadUInt16();
        }
    }
}
