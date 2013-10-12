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
            }
            Console.Title = args[iterator]; // Port number
            var portNumber = short.Parse(args[iterator]);
            var list = new List<short>();
            while (++iterator < args.Length) // All neighbors
                list.Add(short.Parse(args[iterator]));

            connected = new Dictionary<short, Client>();
            server = new Server(portNumber);
            // Create listener
            Task task = new Task(Listen);
            var connectWith = list.Where(x => x > portNumber); // Filter the neighbors with a lower port number
            foreach (var port in connectWith) {
                var client = new Client(port);
                connected.Add(port, client);
            }

            // Set up local graph
            node = new NetChangeNode(portNumber);
            foreach (var port in list) node.AddNeighbor(port);
            node.Updating = true;
            
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e) {
            foreach (var nb in node.neighbors) node.RemoveNeighbor(nb as NetChangeNode);
        }

        static void Listen() {
            var client = server.AcceptConnection();
            var handShake = client.ReadMessage();
            connected.Add(client.ParseHandshake(handShake),client as Client);
        }
    }
}
