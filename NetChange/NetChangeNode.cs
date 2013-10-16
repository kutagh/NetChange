using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                Update(n.value);
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
                builder.AppendFormat("{0}:{1};", kvp.Key.ToString(), kvp.Value.ToString());
            }
            builder.Remove(builder.Length - 1, 1);
            foreach (var neighbor in neighbors)
            {   //a package with update info is a string starting with addressed portNumber and "DistList"
                string package = string.Format("{0};DistList;{1}" ,neighbor.value.ToString(), builder);
            } // Send update
        }

        public void InterpretMess(string package) {
            // convert package back to a distances[portNumber]
            string[] unwrap = package.Split(';');
            short senderNr = short.Parse(unwrap[0]);
            if (senderNr != PortNumber)
            {
                short nextStep = prefNeigh[senderNr];
                //Forward message
            }
            else
            {
                if (unwrap[1] == "DistList")
                    for (int i = 2; i < unwrap.Length; i++)
                    {
                        string[] unpack = unwrap[i].Split(':');
                        if (distances.ContainsKey(short.Parse(unpack[0])))
                        {
                            //only update
                        }
                        else
                        {
                            //add elements and update
                        }
                    }
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
        public void Update(short portNumber) {
            bool hasChanged = false;
            if (portNumber == PortNumber)
            {
                var temp = distances[portNumber];
                temp[portNumber] = 0;
                if (prefNeigh.ContainsKey(portNumber))
                    prefNeigh[portNumber] = portNumber;
                else
                    prefNeigh.Add(portNumber, portNumber);
            }
            else
            {
                KeyValuePair<short, int> d = minDist(distances, portNumber); //d = who to connect to to get given smallest distance
                if (d.Value + 1 < int.MaxValue)
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
                    RemoveNeighbor(portNumber);
                    hasChanged = true;
                }
            }
            if (hasChanged)
            {
                UpdateNeighbors();
            }
            // Update this node and neighbors
        }

        public KeyValuePair<short, int> minDist(Dictionary<short, Dictionary<short, int>> dic1, int targetNr)
        {
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
    }
    
}
