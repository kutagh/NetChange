using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace NetChange {
    abstract class Connection {
        protected StreamWriter writer;
        protected StreamReader reader;
        protected TcpClient client;
        public Connection() {
        }

        protected void finalizeCreation() {
            reader = new StreamReader(client.GetStream());
            writer = new StreamWriter(client.GetStream());
            writer.AutoFlush = true;
        }
    }

    class Server : Connection {
        TcpListener server;

        public Server(short portNumber) {
            server = new TcpListener(IPAddress.Any, portNumber);
            client = server.AcceptTcpClient();
            finalizeCreation();
        }
    }

    class Client : Connection {
        public Client(short portNumber) {
            client = new TcpClient("localhost", portNumber);
            finalizeCreation();
        }
    }
}
