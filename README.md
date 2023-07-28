# NodeNet
A C# framework for building simple TCP based peer to peer networks.

Note: This is a work in progress. Key features are yet to be implemented.

## Example
```
//create a node named node1
var node = new Node("node1");
//configure node1 to accept connections from incoming nodes on port 9000
node.StartListener(9000);
//connect to a node on a remote machine
node.Connect("remotehost", 9000);
//send a message to the connected node
var response = node.Send("hello");
Console.WriteLine(response);
```
## Terminal.csproj
An example of using the Node class in a console application to build a terminal that can connect to other terminals and send/receive messages

## Features & Plan
- Request/Response forwarding (done)
- Timeout detection (done)
- Detection of hard TCP connection termination (done)
- Asychronous TPL support (done)
- Unit Tests (TODO)
- Routing (TODO)
- Failure hardening (TODO)
- Further example programs (TODO)
- TLS between nodes (TODO)
- API design (TODO)
- Security (TODO)
