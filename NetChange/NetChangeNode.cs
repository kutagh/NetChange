﻿using System;
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

        SpinLock distLocker = new SpinLock(), prefLocker = new SpinLock();

        void distLock() {
            if (distLocker.IsHeldByCurrentThread) return;
            var temp = false;
            while (!temp)
                distLocker.Enter(ref temp);
        }
        void prefLock() {
            if (prefLocker.IsHeldByCurrentThread) return;
            var temp = false;
            while (!temp)
                prefLocker.Enter(ref temp);
        }
        void rtLock() {
            distLock();
            prefLock();
        }
        void distUnlock() {
            distLocker.Exit();
        }
        void prefUnlock() {
            prefLocker.Exit();
        }
        void rtUnlock() {
            distUnlock();
            prefUnlock();
        }

        /// <summary>
        /// Constructor for a NetChange Node
        /// </summary>
        /// <param name="portNumber">Port number to be used for this node</param>
        public NetChangeNode(short portNumber, bool startUpdating = false) : base(portNumber) {
            Dictionary<short, int> temp = new Dictionary<short,int>();
            temp.Add(PortNumber, 0);
            rtLock();
            distances.Add(PortNumber, temp);
            prefNeigh.Add(PortNumber, PortNumber);
            rtUnlock();
            Updating = startUpdating;
            UpdateNeighbors();
        }
            
        /// <summary>
        /// Add a neighboring NetChange node
        /// </summary>
        /// <param name="node">NetChange node to add</param>
        public void AddNeighbor(NetChangeNode node) {
            base.AddNeighbor(node);

            Console.WriteLine("Add {0} to {1} listed neighbors", node.value, PortNumber);
            Dictionary<short, int> temp = new Dictionary<short, int>();
            temp.Add(PortNumber, 1);
            distLock();
            if (distances.ContainsKey(node.value))
            {
                distances[node.value] = temp;
            }
            else
            {
                distances.Add(node.value, temp);
            }
            if (distances[PortNumber].ContainsKey(node.value))
            {
                distances[PortNumber][node.value] = 1;
            }
            else
            {
                distances[PortNumber].Add(node.value, 1);
            }
            distUnlock();
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
            distLock();
            foreach (KeyValuePair<short,Dictionary<short, int>> dic1 in distances)
            {   //remove all instances of connections with portNumber in distances
                dic1.Value.Remove(portNumber);
            }
            distances.Remove(portNumber);
            distUnlock(); prefLock();
            foreach (KeyValuePair<short, short> pref in prefNeigh.Where(kvp => kvp.Value == portNumber).ToList())
            {   //remove all preferred connections going through the now possibly non-existent node
                prefNeigh.Remove(pref.Key);
            }
            prefUnlock();
            Update(portNumber);
        }

        /// <summary>
        /// Send an update message to all neighbors
        /// </summary>
        public void UpdateNeighbors() {
            // convert distances[PortNumber] to string representation
            var builder = new StringBuilder();
            distLock();
            foreach (KeyValuePair<short, int> kvp in distances[PortNumber])
            {
                builder.AppendFormat("{0}{1}{2}{3}", entrySeparator, kvp.Key.ToString(), valueSeparator, kvp.Value.ToString());
            }
            distUnlock();
            foreach (var neighbor in neighbors)
            {   //a package with update info is a string starting with addressed portNumber, sender portNumber and "DistList"
                string package = string.Format("{0}{1}{2}{1}{3}{4}", neighbor.value.ToString(), entrySeparator, PortNumber, headerSeparator, builder.ToString());
                Globals.Lock();
                while (!Globals.GetDictionary().Keys.Contains(neighbor.value)) { Globals.Unlock(); Thread.Sleep(5); Globals.Lock(); }
                Globals.Get(neighbor.value).SendMessage(package);
            }           // Sends update
        }

        public string InterpretMess(string package) {
            // convert package back to a distances[portNumber]
            string[] unwrap = package.Split(entrySeparator);
            try
            {
                short senderNr = short.Parse(unwrap[0]);
                short sender = short.Parse(unwrap[1]);
                if (Globals.PrintStatusChanges) Console.WriteLine("Bericht van node {0} voor node {1}", sender, senderNr);
                if (senderNr != PortNumber)
                {
                    prefLock();
                    short nextStep = prefNeigh[senderNr];
                    prefUnlock();
                    Globals.Get(nextStep).SendMessage(package);
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
                        distLock();
                        if (distances.ContainsKey(sender)) distances.Remove(sender); //we throw away the previous list of the sender
                        Dictionary<short, int> temp = new Dictionary<short, int>();  //and build up a fresh one
                        Console.WriteLine(unwrap.Length.ToString());
                        for (int i = 3; i < unwrap.Length; i++)
                        {
                            string[] unpack = unwrap[i].Split(valueSeparator);
                            temp.Add(short.Parse(unpack[0]), int.Parse(unpack[1]));
                            Console.WriteLine("--unwrap of {0}; {1}, {2}", PortNumber, unpack[0], unpack[1]);
                        }
                        distances.Add(sender, temp);
                        distUnlock();
                        foreach (KeyValuePair<short, int> kvp in temp)
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
            catch { return null; }
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
            Console.WriteLine("recompute of {0} for {1}", PortNumber, portNumber);
#endif
            bool hasChanged = false;
            if (portNumber == PortNumber)
            {
                Console.WriteLine("--rec self");
                distLock();
                var temp = distances[portNumber];
                distUnlock();
                temp[portNumber] = 0;
                prefLock();
                if (prefNeigh.ContainsKey(portNumber))
                    prefNeigh[portNumber] = portNumber;
                else prefNeigh.Add(portNumber, portNumber);
                prefUnlock();
                hasChanged = true;
            }
            else if (FindNeighbor(portNumber) != null)
            {
                Console.WriteLine("--rec {0} is neighbor", portNumber);
                distLock();
                if (distances[PortNumber].ContainsKey(portNumber))
                {
                    if (distances[PortNumber][portNumber] != 1)
                    {
                        distances[PortNumber][portNumber] = 1;
                        hasChanged = true;
                    }
                }
                else
                {
                    hasChanged = true;
                    distances[PortNumber].Add(portNumber, 1);
                }
                distUnlock();
                prefLock();
                if (prefNeigh.ContainsKey(portNumber))
                    prefNeigh[portNumber] = portNumber;
                else prefNeigh.Add(portNumber, portNumber);
                prefUnlock();
                
            }
            else
            {
                Console.WriteLine("--rec {0} isn't neigbor", portNumber);
                bool dcontain = false;
                distLock();
                foreach (var kvp in distances)  //is portNumber allready noted as a possible target from anywhere?
                {
                    if (kvp.Value.ContainsKey(portNumber))
                    {
                        dcontain = true;
                        break;
                    }
                }

                if (dcontain)
                {
                    Console.WriteLine("--rec {0} known as possible target", portNumber);
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
                        distUnlock();
                        prefLock();
                        short PRVk = -1;
                        if (prefNeigh.ContainsKey(portNumber))
                        {
                            PRVk = prefNeigh[portNumber];
                            prefNeigh[portNumber] = d.Key;
                        }
                        else prefNeigh.Add(portNumber, d.Key);
                        prefUnlock();
                        if ((PRVd != d.Value + 1) || (PRVk != d.Key))
                            hasChanged = true;
                    }
                    else
                    {
                        if (d.Key > -1)
                        {
                            Console.WriteLine("--rec {0} unreachable", portNumber);
                            RemoveNeighbor(portNumber);
                            distUnlock();
                            hasChanged = true;
                        }
                        else
                        {
                            distUnlock();
                            Console.WriteLine("--rec No MinDist");
                            hasChanged = true;
                        }
                    }
                }
                else
                {
                    if (Globals.ContainsKey(portNumber))
                    {
                        prefLock();
                        distances[PortNumber].Add(portNumber, 1);
                        prefNeigh[portNumber] = portNumber;
                        prefUnlock(); distUnlock();
                        hasChanged = true;
                    }
                    else
                    {
                        distances[PortNumber].Add(portNumber, int.MaxValue);
                        distUnlock();
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
            Console.WriteLine("--minDist by {0}: target {1}", PortNumber, targetNr);
            if (Globals.ContainsKey(targetNr))
                return new KeyValuePair<short, int>(targetNr, 0);
            KeyValuePair<short, int> result = new KeyValuePair<short, int>(-1, int.MaxValue);
            foreach (KeyValuePair<short, Dictionary<short, int>> node1 in dic1)
                if (FindNeighbor(node1.Key) != null)
                {   //if the first of the connection tuples is a neighbor
                    foreach (KeyValuePair<short, int> node2 in node1.Value)
                    {   //get the distance to the target
                        Console.WriteLine("-minD {0}, {1}, {2}", node1.Key, node2.Key, node2.Value);
                        if (node2.Key == targetNr && node2.Value < result.Value)
                        {
                            Console.WriteLine("-minD new result");
                            result = new KeyValuePair<short, int>(node1.Key, node2.Value);
                        } 
                    }
                }
            Console.WriteLine("--minDist output: {0}, {1}", result.Key, result.Value);
            return result; //return the smallest
        }

        internal void PrintRoutingTable() {

            Console.WriteLine("Routing table of {0}:", PortNumber);
            distLock(); prefLock();
            Console.WriteLine("to self ({0}): {1}", PortNumber, distances[PortNumber][PortNumber]);
            foreach (KeyValuePair<short, int> kvp in distances[PortNumber])
            {
                if (kvp.Key != PortNumber)
                {
                    try
                    {
                        Console.WriteLine("to port {0} via {1}: {2}", kvp.Key, prefNeigh[kvp.Key], kvp.Value);
                    }
                    catch
                    {
                        Console.WriteLine("to port {0} via undef: {1}", kvp.Key, kvp.Value);
                    }
                }
            }
            prefUnlock(); distUnlock();
        }
    }
    
}
