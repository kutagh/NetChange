using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetChange {

    /// <summary>
    /// NetChange node
    /// </summary>
    class NetChangeNode : Node<short> {
        /// <summary>
        /// The port number of this node
        /// </summary>
        public short PortNumber { get { return this.value; } }
        public bool Updating { get; set; }
        char entrySeparator = ';';
        char valueSeparator = ':';
        string headerSeparator = "DistList";
        string messheadseparator = "TextMess";

        public Dictionary<short, Dictionary<short, int>> distances = new Dictionary<short, Dictionary<short, int>>();
            //list of known nodes and distances to the others from there
        public Dictionary<short, short> prefNeigh = new Dictionary<short, short>(); 
            //to whom to send messages when it has to go to some node

        /// <summary>
        /// Constructor for a NetChange Node
        /// </summary>
        /// <param name="portNumber">Port number to be used for this node</param>
        public NetChangeNode(short portNumber, bool startUpdating = false) : base(portNumber) {
            Dictionary<short, int> temp = new Dictionary<short,int>();
            temp.Add(PortNumber, 0);
            distances.Add(PortNumber, temp);
            prefNeigh.Add(PortNumber, PortNumber);
            Updating = startUpdating;
            UpdateNeighbors();
        }
            
        /// <summary>
        /// Add a neighboring NetChange node
        /// </summary>
        /// <param name="node">NetChange node to add</param>
        public void AddNeighbor(NetChangeNode node) {
            base.AddNeighbor(node);

            Dictionary<short, int> temp = new Dictionary<short, int>();
            temp.Add(PortNumber, 1);
            if (distances.ContainsKey(node.value))
            {
                distances[node.value] = temp;
            }
            else
            {
                distances.Add(node.value, temp);
            }

            foreach (NetChangeNode n in neighbors)
            {
#if DEBUG
                Console.WriteLine("Recompute" + n.value.ToString());
#endif
                Update(n.value);
            }
        }

        /// <summary>
        /// Add a new neighboring NetChange node
        /// </summary>
        /// <param name="portNumber">Port number of the new neighboring NetChange node</param>
        public override void AddNeighbor(short portNumber) {
            AddNeighbor(new NetChangeNode(portNumber));
        }

        /// <summary>
        /// Remove a neighboring NetChange node
        /// </summary>
        /// <param name="node">NetChange node to remove as neighbor</param>
        public void RemoveNeighbor(NetChangeNode node) {
            base.RemoveNeighbor(node);
            Update(node.value);
        }

        /// <summary>
        /// Remove a neighboring NetChange node
        /// </summary>
        /// <param name="portNumber">Port number of neighbor to remove</param>
        public override void RemoveNeighbor(short portNumber) {
            base.RemoveNeighbor(portNumber);
            foreach (KeyValuePair<short,Dictionary<short, int>> dic1 in distances)
            {   //remove all instances of connections with portNumber in distances
                dic1.Value.Remove(portNumber);
            }
            distances.Remove(portNumber);
            foreach (KeyValuePair<short, short> pref in prefNeigh.Where(kvp => kvp.Value == portNumber).ToList())
            {   //remove all preferred connections going through the now possibly non-existent node
                prefNeigh.Remove(pref.Key);
            }
            Update(portNumber);
        }

        /// <summary>
        /// Send an update message to all neighbors
        /// </summary>
        public void UpdateNeighbors() {
            // convert distances[PortNumber] to string representation
            var builder = new StringBuilder();
            foreach (KeyValuePair<short, int> kvp in distances[PortNumber])
            {
                builder.AppendFormat("{0}{1}{2}{3}", entrySeparator, kvp.Key.ToString(), valueSeparator, kvp.Value.ToString());
            }
            foreach (var neighbor in neighbors)
            {   //a package with update info is a string starting with addressed portNumber, sender portNumber and "DistList"
                string package = string.Format("{0}{1}{2}{1}{3}{4}", neighbor.value.ToString(), entrySeparator, PortNumber, headerSeparator, builder.ToString());
                while (!Globals.connected.Keys.Contains(neighbor.value)) Thread.Sleep(5);
                Globals.connected[neighbor.value].SendMessage(package);
            }           // Sends update
        }

        public string InterpretMess(string package) {
            // convert package back to a distances[portNumber]
            string[] unwrap = package.Split(entrySeparator);
            short senderNr = short.Parse(unwrap[0]);
            short sender = short.Parse(unwrap[1]);
            if (Globals.PrintStatusChanges) Console.WriteLine("Bericht van node {0} voor node {1}", sender, senderNr);
            if (senderNr != PortNumber)
            {
                short nextStep = prefNeigh[senderNr];
                Globals.connected[nextStep].SendMessage(package);
                        // Forwards message
                if (Globals.PrintStatusChanges) Console.WriteLine("Bericht voor node {0} verstuurd naar node {1}", senderNr, prefNeigh);
                return null;
            }
            else
            {
                if (unwrap[2] == headerSeparator)
                {
#if DEBUG
                    Console.WriteLine("unwrapped DistList");
#endif
                    if (distances.ContainsKey(sender)) distances.Remove(sender); //we throw away the previous list of the sender
                    Dictionary<short, int> temp = new Dictionary<short, int>();  //and build up a fresh one
                    for (int i = 2; i < unwrap.Length; i++)
                    {
                        string[] unpack = unwrap[i].Split(valueSeparator);
                        temp.Add(short.Parse(unpack[0]), int.Parse(unpack[1]));
                    }
                    distances.Add(sender, temp);
                    foreach (KeyValuePair<short, int> kvp in distances[sender])
                        Update(kvp.Key);                                        //we also update the connections the sender knew of
                    return null;
                }
                else if (unwrap[2] == messheadseparator)
                {
                    return unwrap[3];
                }
                else throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Find a neighboring NetChange node
        /// </summary>
        /// <param name="portNumber">Port number of the neighbor you want to find</param>
        /// <returns>NetChangeNode if it is a neighbor, null if it isn't</returns>
        public new NetChangeNode FindNeighbor(short portNumber) {
            return base.FindNeighbor(portNumber) as NetChangeNode;
        }

        /// <summary>
        /// Update this NetChange node
        /// </summary>
        public void Update(short portNumber) {  //recompute
#if DEBUG
            Console.WriteLine("recompute");
#endif
            bool hasChanged = false;
            if (portNumber == PortNumber)
            {
                var temp = distances[portNumber];
                temp[portNumber] = 0;
                if (prefNeigh.ContainsKey(portNumber))
                    prefNeigh[portNumber] = portNumber;
                else prefNeigh.Add(portNumber, portNumber);
                hasChanged = true;
            }
            else if (Globals.connected.ContainsKey(portNumber))
            {
                if (distances[PortNumber].ContainsKey(portNumber))
                    distances[PortNumber][portNumber] = 1;
                else distances[PortNumber].Add(portNumber, 1);
                if (prefNeigh.ContainsKey(portNumber))
                    prefNeigh[portNumber] = portNumber;
                else prefNeigh.Add(portNumber, portNumber);
                hasChanged = true;
            }
            else
            {
                bool dcontain = false;
                foreach (var kvp in distances)
                {
                    if (kvp.Value.ContainsKey(portNumber))
                    {
                        dcontain = true;
                        break;
                    }
                }
                if (dcontain)
                {
                    KeyValuePair<short, int> d = minDist(distances, portNumber); //d = who to connect to to get given smallest distance
                    if (d.Value < int.MaxValue - 1)
                    {
                        int PRVd = -1;
                        if (distances[PortNumber].ContainsKey(portNumber))
                        {
                            PRVd = distances[PortNumber][portNumber];
                            distances[PortNumber][portNumber] = d.Value + 1;
                        }
                        else distances[PortNumber].Add(portNumber, d.Value + 1);

                        short PRVk = -1;
                        if (prefNeigh.ContainsKey(portNumber))
                        {
                            PRVk = prefNeigh[portNumber];
                            prefNeigh[portNumber] = d.Key;
                        }
                        else prefNeigh.Add(portNumber, d.Key);

                        if ((PRVd != d.Value + 1) || (PRVk != d.Key))
                            hasChanged = true;
                    }
                    else
                    {
                        PrintRoutingTable();
                        Thread.Sleep(1000);
                        //RemoveNeighbor(portNumber);
                        hasChanged = true;
                    }
                }
                else
                {
                    if (Globals.connected.ContainsKey(portNumber))
                    {
                        distances[PortNumber].Add(portNumber, 1);
                        prefNeigh[portNumber] = portNumber;
                        hasChanged = true;
                    }
                    else
                    {
                        distances[PortNumber].Add(portNumber, int.MaxValue);
                        Update(portNumber);
                        hasChanged = true;
                    }
                }
            }
            if (hasChanged)
            {
                UpdateNeighbors();
            }
        }   // Updates this node and it's neighbors

        public KeyValuePair<short, int> minDist(Dictionary<short, Dictionary<short, int>> dic1, short targetNr)
        {
            if (Globals.connected.ContainsKey(targetNr))
                return new KeyValuePair<short, int>(targetNr, 0);
            KeyValuePair<short, int> result = new KeyValuePair<short, int>(-1, int.MaxValue);
            foreach (KeyValuePair<short, Dictionary<short, int>> node1 in dic1)
                if (FindNeighbor(node1.Key) != null)
                {   //if the first of the connection tuples is a neighbor
                    foreach (KeyValuePair<short, int> node2 in node1.Value)
                    {   //get the distance to the target
                        if (node2.Key == targetNr && node2.Value < result.Value)
                        {
                            result = new KeyValuePair<short, int>(node1.Key, node2.Value);
                        } 
                    }
                }
            return result; //return the smallest
        }

        internal void PrintRoutingTable() {

            Console.WriteLine("Routing table of {0}:", PortNumber);
            Console.WriteLine("to self ({0}): {1}", PortNumber, distances[PortNumber][PortNumber]);
            foreach (KeyValuePair<short, int> kvp in distances[PortNumber])
            {
                if (kvp.Key != PortNumber)
                    Console.WriteLine("to port {0} via {1}: {2}", kvp.Key, //prefNeigh[kvp.Key], 
                        10, kvp.Value);
            }
        }
    }
    
}
