using System;
using System.IO;

namespace NodeNet
{
    internal class ProtocolWriter
    {
        BinaryWriter writer;
        internal ProtocolWriter(Stream stream)
        {
            writer = new BinaryWriter(stream);
        }

        public void WriteMessage(Message message)
        {
            switch (message.Type)
            {
                case MessageType.Request:
                    WriteRequestMessage(message as RequestMessage);
                    break;
                case MessageType.Response:
                    WriteResponseMessage(message as ResponseMessage);
                    break;
                case MessageType.KeepAlive:
                    WriteKeepAliveMessage(message as KeepAliveMessage);
                    break;
                default:
                    throw new ProtocolException($"Attempted to write unknown message type {message.Type}");
            }
        }

        private void WriteKeepAliveMessage(KeepAliveMessage keepAliveMessage)
        {
            WriteMessageType(MessageType.KeepAlive);
        }

        void WriteRequestMessage(RequestMessage message)
        {
            WriteMessageType(MessageType.Request);
            writer.Write((uint)message.RequestId);
            if (message.TargetNodeName != null)
            {
                var data = System.Text.Encoding.ASCII.GetBytes(message.TargetNodeName);
                writer.Write((byte)data.Length);
                if (data.Length > 0)
                    writer.Write(data);
            }
            else
            {
                writer.Write((byte)255);
                writer.Write(message.TargetNodeId.ToByteArray());
            }
            writer.Write((uint)message.RequestData.Length);
            writer.Write(message.RequestData);
        }

        void WriteResponseMessage(ResponseMessage message)
        {
            WriteMessageType(MessageType.Response);
            writer.Write((uint)message.RequestId);
            writer.Write((byte)message.RequestResult);
            writer.Write((uint)message.ResponseData.Length);
            writer.Write(message.ResponseData);
        }

        void WriteMessageType(MessageType type)
        {
            writer.Write((byte)type);
        }

        internal void WriteData16(byte[] data)
        {
            writer.Write((ushort)data.Length);
            writer.Write(data);
        }

        internal void WriteData8(byte[] data)
        {
            if (data.Length > 255)
                throw new Exception($"Too much data {data.Length} for WriteData8");
            writer.Write((byte)data.Length);
            writer.Write(data);
        }
        internal void WriteBytes(byte[] data)
        {
            writer.Write(data);
        }

        internal void WriteUInt16(ushort value)
        {
            writer.Write(value);
        }

        internal void WriteByte(byte n)
        {
            WriteBytes(new byte[] { n });
        }
    }
}
