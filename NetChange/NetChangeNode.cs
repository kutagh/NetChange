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

        public Dictionary<short, Dictionary<short, int>> distances = new Dictionary<short, Dictionary<short, int>>();     //list of known/connected nodes and distances to the others from there
        public Dictionary<short, short> prefNeigh = new Dictionary<short, short>(); //to whom to send messages when it has to go to some node

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

            Dictionary<short, int> temp = new Dictionary<short, int>();
            temp.Add(PortNumber, 1);
            if (distances.ContainsKey(portNumber))
            {
                distances[portNumber] = temp;
            }
            else
            {
                distances.Add(portNumber, temp);
            }
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
            Update(portNumber);
        }

        /// <summary>
        /// Send an update message to all neighbors
        /// </summary>
        public void UpdateNeighbors() {
            foreach (var neighbor in neighbors) { } // Send update
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
            if (portNumber == PortNumber)
            {
                var temp = distances[portNumber];
                temp[portNumber] = 0;
                prefNeigh[portNumber] = portNumber;
            }
            else
            {
                KeyValuePair<short, int> d = minDist(distances[portNumber]);
                if (d.Value + 1 < int.MaxValue)
                {
                    distances[PortNumber][portNumber] = d.Value + 1;
                    prefNeigh[portNumber] = d.Key;
                }
            }
            // Update this node and neighbors
        }

        public KeyValuePair<short, int> minDist(Dictionary<short, int> dict)
        {
            KeyValuePair<short, int> result = new KeyValuePair<short, int>(short.MaxValue,int.MaxValue);
            foreach (KeyValuePair<short, int> node in dict)
                if (node.Value < result.Value) result = node;
            return result;
        }
    }
    
}
