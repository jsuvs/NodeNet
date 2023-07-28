using System;

namespace NodeNet
{
    internal class NodeInfo
    {
        internal Guid Id { get; set; }
        internal string Name { get; set; }
        internal NodeCapabilities Capabilities { get; set; }
    }
}

