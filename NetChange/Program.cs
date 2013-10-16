using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetChange {
    class NetwProg {
        static NetChangeNode node;
        static Server server;
        static Dictionary<short,Client> connected;
        static void Main(string[] args) {
            if (args.Length == 0) {
                args = new string[] { "1000", "1001" };
            }
            int iterator = 0;
            if (args[0][0] == 'p') {
                // Set console position
                Console.SetWindowPosition(int.Parse(args[0].Substring(1)), int.Parse(args[1].Substring(1)));
                iterator += 2;
                Console.Title = string.Format("port = {0}, x = {1}, y = {2}", args[iterator], args[0].Substring(1), args[1].Substring(1)); // Port number
            }
            else {
                Console.Title = string.Format("port = {0}", args[iterator]); // Port number
            }
            var myPortNumber = short.Parse(args[iterator]);
            var list = new List<short>();
            while (++iterator < args.Length) // All neighbors
                list.Add(short.Parse(args[iterator]));

            connected = new Dictionary<short, Client>();
            server = new Server(myPortNumber);
            // Create listener
            Thread th = new Thread(new ThreadStart(Listen));
            th.Start();
            //Task task = new Task(Listen);
            //task.Start();
            var connectWith = list.Where(x => x > myPortNumber); // Filter the neighbors with a lower port number
            foreach (var port in connectWith) {
                bool retry = true;
                int attempt = 0;
                while (retry) {
                    try {
                        var client = new Client(port);
                        connected.Add(port, client);
                        retry = false;
                    }
                    catch {
#if DEBUG
                        Console.WriteLine("Failed to connect, retrying for {0}th time", attempt++);
#endif
                        System.Threading.Thread.Sleep(3);
                    }
                }
            }
            
            // Set up local graph
            node = new NetChangeNode(myPortNumber);
            foreach (var port in list) node.AddNeighbor(port);
            node.Updating = true;
            foreach (var client in connected) {
#if DEBUG
                Console.WriteLine("Have{0} connected to {1}", client.Value.IsConnected ? "" : "n't", client.Key);
#endif
                var c = client.Value;
                Thread listener = new Thread(new ThreadStart(() => ListenForMessages(c)));
                listener.Start();
                //Task.Factory.StartNew(() => ListenForMessages(c));
            }
            //AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
#if DEBUG
            foreach (var client in connected) { client.Value.SendMessage("Test"); Console.WriteLine("Sent message to {0}", client.Key); }
            Console.ReadLine();
#endif
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e) {
            foreach (var nb in node.neighbors) node.RemoveNeighbor(nb as NetChangeNode);

        }

        static void Listen() {
#if DEBUG   
            Console.WriteLine("Listening");
#endif
            var client = server.AcceptConnection() as Client;
#if DEBUG   
            Console.WriteLine("Client connected");
#endif
            var handShake = client.ReadMessage();
#if DEBUG   
            Console.WriteLine("Handshake message: " + handShake);
#endif
            var port = client.ParseHandshake(handShake);
#if DEBUG   
            Console.WriteLine("Adding to list of connected clients");
#endif
            connected.Add(port, client);
#if DEBUG   
            Console.WriteLine("Starting to listen for messages from {0}", port);
#endif
            Thread listener = new Thread(new ThreadStart(() => ListenForMessages(client)));
            listener.Start();
            
            //Task.Factory.StartNew(() => ListenForMessages(client));
#if DEBUG
            Console.WriteLine("Accepted connection");
#endif
            Listen();
        }

        static void ListenForMessages(Client c) {
#if DEBUG
            Console.WriteLine("Listening for messages from {0}", c.ConnectedTo);
#endif
            var message = c.ReadMessage();
#if DEBUG
            Console.WriteLine(message);
#endif
            // Handle messages
            ListenForMessages(c);
        }
    }
}
