using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace NetChange {

    /// <summary>
    /// Basic connection class
    /// </summary>
    abstract class Connection {
        protected StreamWriter writer;
        protected StreamReader reader;
        protected TcpClient client;
        protected string handshake = "Connecting from ";
        public bool IsConnected { get { return client != null; } }
        public short ConnectedTo { get; protected set; }
        
        /// <summary>
        /// Once a client is set, call this to create the stream reader and writer
        /// </summary>
        protected void finalizeCreation() {
            reader = new StreamReader(client.GetStream());
            writer = new StreamWriter(client.GetStream());
            writer.AutoFlush = true;
        }

        /// <summary>
        /// Creates a connection as a client to the specified port number on localhost
        /// </summary>
        /// <param name="portNumber">The port number of the host on localhost</param>
        /// <returns>A connection to the host</returns>
        public static Connection ConnectTo(short portNumber) {
            return new Client(portNumber);
        }

        public string ReadMessage() {
            Console.WriteLine("Reading");
            var message = reader.ReadLine();
            Console.Write("Have received a message: ");
            Console.WriteLine(message);
            if (message == null) { Console.WriteLine("Error"); return ""; }
            return message;
        }

        public void SendMessage(string message) {
            writer.Write(message);
        }

        public string CreateHandshake(short portNumber) {
            return string.Format("{0}{1}", handshake, portNumber);
        }

        public short ParseHandshake(string message) {
            if (message.StartsWith(handshake))
                return short.Parse(message.Substring(handshake.Length));
            else
                return -1;
        }
    }

    /// <summary>
    /// Class to accept socket connections
    /// </summary>
    class Server {
        public TcpListener server;

        /// <summary>
        /// Creates a listener
        /// </summary>
        /// <param name="portNumber">Port number to listen on</param>
        public Server(short portNumber) {
            server = new TcpListener(IPAddress.Any, portNumber);
            server.Start();
        }

        /// <summary>
        /// Accept the next incoming connection
        /// </summary>
        /// <returns>A connection</returns>
        public Connection AcceptConnection() {
            Console.WriteLine("Listening for connection");
            return new Client(server.AcceptTcpClient());
        }
    }

    /// <summary>
    /// Client class implementing the abstract Connection class
    /// </summary>
    class Client : Connection {
        /// <summary>
        /// Create a client that attempts to connect to a specified host
        /// </summary>
        /// <param name="portNumber">The port number of the host to connect to</param>
        public Client(short portNumber) {
            bool retry = true;
            for (int i = 0; i < 1000 && retry; i++) {
                retry = false;
                try {
                    client = new TcpClient("localhost", portNumber); // new TcpClient(new IPEndPoint(new IPAddress(new byte[]{127,0,0,1}), portNumber));
                    finalizeCreation(); 
                    SendMessage(CreateHandshake(portNumber));
                    Console.WriteLine("Connected to {0}", portNumber);
                    ConnectedTo = portNumber;
                }
                catch    {
                    retry = true;
                }
            }
            if (retry)
                throw new TimeoutException();
        }

        /// <summary>
        /// Create a wrapper for the client
        /// </summary>
        /// <param name="client">The TcpClient that has a connection to the host</param>
        public Client(TcpClient client) {
            this.client = client;
            finalizeCreation();
        }
    }
}
