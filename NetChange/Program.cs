using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetChange {
    class NetwProg {
        static NetChangeNode node;
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

            // Set up local graph
            var connectWith = list.Where(x => x > portNumber); // Filter the neighbors with a lower port number
            // Connect to all neighbors with a higher port number (connectWith)

            node = new NetChangeNode(portNumber);
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e) {
            foreach (var nb in node.neighbors) node.RemoveNeighbor(nb as NetChangeNode);
        }
    }
}
