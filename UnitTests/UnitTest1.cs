using Microsoft.VisualStudio.TestTools.UnitTesting;
using NodeNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            int testPort = 12312;
            var nodeA = new TestNode("A");
            nodeA.Node.StartListener(testPort);
            var nodeB = new TestNode("B");
            nodeB.Node.Connect("localhost", testPort);
            Assert.IsTrue(nodeA.Events.Contains(TraceEventId.HandshakeSuccess));
            Assert.IsTrue(nodeB.Events.Contains(TraceEventId.HandshakeSuccess));
        }

        [TestMethod]
        public void TestSendTimeout()
        {
            int testPort = 12311;
            var nodeA = new TestNode("A");
            nodeA.Node.StartListener(testPort);
            var nodeB = new TestNode("B");
            nodeB.Node.Connect("localhost", testPort);
            var response = nodeA.Node.Send(new byte[] { 1, 2, 3 });
            Assert.IsTrue(response.Status == ResponseStatus.Timeout);
        }

        [TestMethod]
        public void TestSend()
        {
            int testPort = 12312;
            var nodeA = new TestNode("A");
            nodeA.Node.StartListener(testPort);
            var nodeB = new TestNode("B");
            nodeB.Node.OnRequestReceived += Node_OnRequestReceived;
            nodeB.Node.Connect("localhost", testPort);
            var response = nodeA.Node.Send(new byte[] { 1, 2, 3 });
            Assert.IsTrue(response.Status == ResponseStatus.Success);
        }

        [TestMethod]
        public async Task TestAsyncSend()
        {
            int testPort = 12313;
            var nodeA = new TestNode("A");
            nodeA.Node.StartListener(testPort);
            var nodeB = new TestNode("B");
            nodeB.Node.OnRequestReceived += Node_OnRequestReceived;
            nodeB.Node.Connect("localhost", testPort);
            var response = await nodeA.Node.SendAsync(new byte[] { 1, 2, 3 });
            Assert.IsTrue(response.Status == ResponseStatus.Success);
        }

        private byte[] Node_OnRequestReceived(byte[] requestData)
        {
            return new byte[] { 5, 6, 7 };
        }

        [TestMethod]
        public async Task TestForward()
        {
            //node B sends a message to node C via node A
            int testPort = 12314;
            var nodeA = new TestNode("A");
            nodeA.Node.StartListener(testPort);
            var nodeB = new TestNode("B");
            nodeB.Node.Connect("localhost", testPort);
            var nodeC = new TestNode("C");
            nodeC.Node.OnRequestReceived += Node_OnRequestReceived;
            nodeC.Node.Connect("localhost", testPort);

            var response = await nodeB.Node.SendAsync(new byte[] { 1, 2, 3 }, "C");
            Assert.IsTrue(response.Status == ResponseStatus.Success);
            Assert.IsTrue(response.Data.SequenceEqual(new byte[] { 5, 6, 7 }));
            Assert.IsTrue(nodeA.Events.Contains(TraceEventId.ForwardMessage));
            Assert.IsTrue(nodeA.Events.Contains(TraceEventId.ForwardResponse));
        }

        [TestMethod]
        public void TestGetConnectedNodeInfo()
        {
            int testPort = 12315;
            var nodeA = new TestNode("A");
            nodeA.Node.StartListener(testPort);
            var nodeB = new TestNode("B");
            nodeB.Node.Connect("localhost", testPort); 
            var nodeC = new TestNode("C");
            nodeC.Node.Connect("localhost", testPort);
            var list = nodeA.Node.GetConnectedNodeInfo();
            Assert.AreEqual(2, list.Count);
        }

        class TestNode
        {
            internal Node Node { get; private set; }
            internal List<TraceEventId> Events { get; private set; } = new List<TraceEventId>();
            public TestNode(string name)
            {
                Node = new Node(name);
                Node.OnTraceEvent += (eventId, args) =>
                {
                    Events.Add(eventId);
                };
            }
        }
    }
}
