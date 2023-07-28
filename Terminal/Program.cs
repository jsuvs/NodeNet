using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NodeNet;

namespace Terminal
{
    /// <summary>
    /// Example use of Node class to create a terminal that can connect and send/receive messages to/from other terminals
    /// </summary>
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: Terminal.exe <nodename>");
                return;
            }
            var nodeName = args[0];
            var p = new Program();
            p.Run(nodeName);
        }

        Node node;
        void Run(string nodeName)
        {
            RegisterCommands();
            node = new Node(nodeName);
            node.OnReceiveRequest += Node_OnReceiveRequest;
            node.OnTraceEvent += Node_OnTraceEvent;
            while (true)
            {
                Console.Write("> ");
                var command = Console.ReadLine();
                ProcessCommand(command);
            }
        }

        private byte[] Node_OnReceiveRequest(byte[] requestData)
        {
            var text = Encoding.ASCII.GetString(requestData);
            Console.WriteLine($"received: {text}");
            //echo the command back with ok appended
            var response = Encoding.ASCII.GetBytes(text + " ok");
            return response;
        }

        private void Node_OnTraceEvent(TraceEventId eventId, string arguments)
        {
            Console.WriteLine($"[trace] {eventId} {arguments}");
        }

        void ProcessCommand(string command)
        {
            var parts = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                Console.WriteLine("unknown command");
                return;
            }
            command = parts[0].ToLower();
            if (commands.ContainsKey(command))
                commands[command](parts.Skip(1).ToArray());
            else
            {
                Console.WriteLine("Unknown command");
                PrintUsage();
            }
        }

        Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();

        void Connect(string[] args)
        {
            int port;
            if (args.Length < 2 || !int.TryParse(args[1], out port))
            {
                Console.WriteLine("usage: connect <host> <port>");
                return;
            }
            string host = args[0];
            node.Connect(host, port);
        }

        void Listen(string[] args)
        {
            int port;
            if (args.Length < 1 || !int.TryParse(args[0], out port))
            {
                Console.WriteLine("usage: listen <port>");
                return;
            }
            node.StartListener(port);
        }

        void Send(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("usage: send <command> [destination]");
                return;
            }
            string command = args[0];
            string destination = args.Length >= 2 ? args[1] : null;
            try
            {
                var response = node.Send(Encoding.ASCII.GetBytes(command), destination);
                Console.WriteLine(GetResponseText(response));
            }
            catch (Exception e)
            {
                Console.WriteLine($"error {e.ToString()}");
            }
            
        }

        string GetResponseText(Response response)
        {
            switch (response.Status)
            {
                case ResponseStatus.Success:
                    return $"Received: {Encoding.ASCII.GetString(response.Data)}";
                case ResponseStatus.Timeout:
                    return $"Timeout";
                case ResponseStatus.ResolveFailure:
                    return $"Resolve error";
                case ResponseStatus.UnknownError:
                default:
                    return $"Unknown error";
            }
        }

        void RegisterCommands()
        {
            commands["connect"] = Connect;
            commands["listen"] = Listen;
            commands["send"] = Send;
        }

        void PrintUsage()
        {
            string output = "Available commands: [ ";
            foreach (var c in commands.Keys)
            {
                output += c + " ";
            }
            output += "]";
            Console.WriteLine(output);
        }
    }
}
