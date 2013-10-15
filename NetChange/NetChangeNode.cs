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

        public Dictionary<short, int> distances = new Dictionary<short, int>();     //list of known/connected nodes and distances to the others from there
        public Dictionary<short, short> prefNeigh = new Dictionary<short, short>(); //to whom to send messages when it has to go to some node

        /// <summary>
        /// Constructor for a NetChange Node
        /// </summary>
        /// <param name="portNumber">Port number to be used for this node</param>
        public NetChangeNode(short portNumber, bool startUpdating = false) : base(portNumber) {
            Updating = startUpdating;
        }
            
        /// <summary>
        /// Add a neighboring NetChange node
        /// </summary>
        /// <param name="node">NetChange node to add</param>
        public void AddNeighbor(NetChangeNode node) {
            base.AddNeighbor(node);

            if (distances.ContainsKey(node.value))
            {
                distances[node.value] = 1;
            }
            else
            {
                distances.Add(node.value, 1);
            }

            Update();
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
            Update();
        }

        /// <summary>
        /// Remove a neighboring NetChange node
        /// </summary>
        /// <param name="portNumber">Port number of neighbor to remove</param>
        public override void RemoveNeighbor(short portNumber) {
            base.RemoveNeighbor(portNumber);
            Update();
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
        public void Update() {
            // Update this node and neighbors
        }
    }
    
}
