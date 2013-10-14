using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetChange {
    class NetwProg {
        static NetChangeNode node;
        static Server server;
        static Dictionary<short,Client> connected;
        static void Main(string[] args) {

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
            Task task = new Task(Listen);
            task.Start();
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
                        Console.WriteLine("Failed to connect, retrying for {0}th time", attempt++);
                        System.Threading.Thread.Sleep(3);
                    }
                }
            }
            
            // Set up local graph
            node = new NetChangeNode(myPortNumber);
            foreach (var port in list) node.AddNeighbor(port);
            node.Updating = true;
            foreach (var client in connected) {
                Console.WriteLine("Have{0} connected to {1}", client.Value.IsConnected ? "" : "n't", client.Key);
                var c = client.Value;
                Task.Factory.StartNew(() => ListenForMessages(c));
            }
            //AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            foreach (var client in connected) { client.Value.SendMessage("Test"); Console.WriteLine("Sent message to {0}", client.Key); }
            Console.ReadLine();
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e) {
            foreach (var nb in node.neighbors) node.RemoveNeighbor(nb as NetChangeNode);

        }

        static void Listen() {
            var client = server.AcceptConnection();
            var handShake = client.ReadMessage();
            connected.Add(client.ParseHandshake(handShake), client as Client);
            Console.WriteLine("Accepted connection");
            Listen();
        }

        static void ListenForMessages(Client c) {
            Console.WriteLine(c.ReadMessage());
            ListenForMessages(c);
        }
    }
}
