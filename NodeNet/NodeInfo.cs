using System;
using System.Net;

namespace NodeNet
{
    public class NodeInfo
    {
        public EndPoint Endpoint { get; internal set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public NodeCapabilities Capabilities { get; set; }
    }
}

