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
            var message = reader.ReadLine();
            if (message == null) return "";
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
        }

        /// <summary>
        /// Accept the next incoming connection
        /// </summary>
        /// <returns>A connection</returns>
        public Connection AcceptConnection() {
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
            client = new TcpClient("localhost", portNumber);
            finalizeCreation();
            SendMessage(CreateHandshake(portNumber));
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
