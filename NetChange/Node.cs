using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetChange {

    /// <summary>
    /// A node in a bidirectional graph
    /// </summary>
    /// <typeparam name="T">Type of the value of the node</typeparam>
    class Node<T> {
        internal T value;
        internal List<Node<T>> neighbors;
        SpinLock nbLocker = new SpinLock();

        void nbLock() {
            var temp = false;
            nbLocker.Enter(ref temp);
        }

        void nbUnlock() {
            nbLocker.Exit();
        }

        /// <summary>
        /// Constructor for a node in a bidirectional graph
        /// </summary>
        /// <param name="value">The value of the node</param>
        public Node(T value) {
            neighbors = new List<Node<T>>();
            this.value = value;
        }

        /// <summary>
        /// Adds a neighbor to the node
        /// </summary>
        /// <param name="node">neighbor to add to the graph</param>
        public virtual void AddNeighbor(Node<T> node, bool sendToNB = true) {
            nbLock();
            this.neighbors.Add(node);
            nbUnlock();
            node.AddNeighbor(this, false);
        }

        /// <summary>
        /// Adds a new neighbor to the node
        /// </summary>
        /// <param name="value">New neighbor with value to be added</param>
        public virtual void AddNeighbor(T value) {
            AddNeighbor(new Node<T>(value));
        }

        /// <summary>
        /// Remove a neighbor from the network
        /// </summary>
        /// <param name="node">neighbor to be removed</param>
        public virtual void RemoveNeighbor(Node<T> node, bool sendToNB = true) {
            nbLock();
            this.neighbors.Remove(node);
            nbUnlock();
            node.RemoveNeighbor(this, false);
        }

        /// <summary>
        /// Remove a neighbor from the network if it is a neighbor
        /// </summary>
        /// <param name="value">Value of neighbor to remove</param>
        public virtual void RemoveNeighbor(T value) {
            var toRemove = FindNeighbor(value);
            if (toRemove == null) return; // Safety check so we don't remove a neighbor that isn't a neighbor
            RemoveNeighbor(toRemove);
            toRemove.RemoveNeighbor(this, false);
        }

        /// <summary>
        /// Check for and retrieve a neighbor of this node
        /// </summary>
        /// <param name="value">Value of neighbor</param>
        /// <returns>neighbor node if it exists, null if it isn't a neighbor</returns>
        public virtual Node<T> FindNeighbor(T value) {
            nbLock();
            foreach (var neighbor in neighbors)
                if (neighbor.value.Equals(value)) {
                    nbUnlock();
                    return neighbor;
                }
            nbUnlock();
            return null;
        }
    }
}
