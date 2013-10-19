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
            //list of known nodes and distances to the others from there (whose list(1), connected to(2), distance from 1 to 2)
        public Dictionary<short, short> prefNeigh = new Dictionary<short, short>(); 
            //to whom to send messages when it has to go to some node (who you need, where to send it)

        ImprovedSpinlock distLocker = new ImprovedSpinlock(), prefLocker = new ImprovedSpinlock();

        void distLock() {
            distLocker.Lock();
        }
        void prefLock() {
            prefLocker.Lock();
        }
        void rtLock() {
            distLock();
            prefLock();
        }
        void distUnlock() {
            distLocker.Unlock();
        }
        void prefUnlock() {
            prefLocker.Unlock();
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
#if DEBUG
            Console.WriteLine("Add {0} to {1} listed neighbors", node.value, PortNumber);
#endif
            Dictionary<short, int> temp = new Dictionary<short, int>();
            temp.Add(PortNumber, int.MaxValue);
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
                distances[PortNumber][node.value] = int.MaxValue;
            }
            else
            {
                distances[PortNumber].Add(node.value, int.MaxValue);
            }
            distUnlock();
            if (!Updating) return;
            nbLock();
            foreach (NetChangeNode n in neighbors)
            {
#if DEBUG
                Console.WriteLine("Recompute" + n.value.ToString());
#endif
                Update(n.value);
            }
            nbUnlock();
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
            RemoveNeighbor(node.value);
        }

        /// <summary>
        /// Remove a neighboring NetChange node
        /// </summary>
        /// <param name="portNumber">Port number of neighbor to remove</param>
        public override void RemoveNeighbor(short portNumber) {
            if (portNumber == PortNumber)   //DO NOT REMOVE YOURSELF DAMNIT
                return;
            base.RemoveNeighbor(portNumber);
            distLock();
            distances[PortNumber].Remove(portNumber);
            distances.Remove(portNumber);
            distUnlock(); prefLock();
            var recomList = prefNeigh.Where(kvp => kvp.Value == portNumber).ToList();
            foreach (KeyValuePair<short, short> pref in recomList)
            {   //remove all preferred connections going through the now possibly non-existent node
                prefNeigh.Remove(pref.Key);
            }
            prefNeigh.Remove(portNumber);
            prefUnlock();
            nbLock();
            foreach (var node in neighbors)
                Update(node.value);
            nbUnlock();
            foreach (var kvp in recomList)
                Update(kvp.Key);
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
            int distanceEstimatesSent = 0;
            nbLock();
            foreach (var neighbor in neighbors)
            {   //a package with update info is a string starting with addressed portNumber, sender portNumber and "DistList"
                string package = string.Format("{0}{1}{2}{1}{3}{4}", neighbor.value.ToString(), entrySeparator, PortNumber, headerSeparator, builder.ToString());
                Globals.Lock();
                while (!Globals.GetDictionary().Keys.Contains(neighbor.value)) { Globals.Unlock(); Thread.Sleep(5); Globals.Lock(); }
                Globals.Get(neighbor.value).SendMessage(package);
                Globals.Unlock();
                distanceEstimatesSent++;
            }           // Sends update
            nbUnlock();
            Globals.IncrementTotalDistanceEstimatesSent(distanceEstimatesSent);
        }

        public string InterpretMess(string package) {
            // convert package back to a distances[portNumber]
            string[] unwrap = package.Split(entrySeparator);
            try
            {
                short sentTo = short.Parse(unwrap[0]);
                short sender = short.Parse(unwrap[1]);
                if (Globals.PrintStatusChanges) Console.WriteLine("Bericht van node {0} voor node {1}", sender, sentTo);
                if (sentTo != PortNumber)
                {
                    prefLock();
                    short nextStep = prefNeigh[sentTo];
                    prefUnlock();
                    Globals.Get(nextStep).SendMessage(package);
                    // Forwards message
                    if (Globals.PrintStatusChanges) Console.WriteLine("Bericht voor node {0} verstuurd naar node {1}", sentTo, prefNeigh);
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
#if DEBUG
                        Console.WriteLine(unwrap.Length.ToString());
#endif
                        for (int i = 3; i < unwrap.Length; i++)
                        {
                            string[] unpack = unwrap[i].Split(valueSeparator);
                            temp.Add(short.Parse(unpack[0]), int.Parse(unpack[1]));
#if DEBUG
                            Console.WriteLine("--unwrap of {0}; {1}, {2}", PortNumber, unpack[0], unpack[1]);
#endif
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

        public void Update() {
            nbLock();
            foreach (var node in neighbors) {
                Update(node.value);
            }
            nbUnlock();
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
#if DEBUG
                Console.WriteLine("--rec self");
#endif
                distLock();
                if (distances.ContainsKey(portNumber))
                    if (distances[portNumber].ContainsKey(portNumber))
                        distances[portNumber][portNumber] = 0;
                    else distances[portNumber].Add(portNumber, 0);
                else
                {
                    var temp = new Dictionary<short, int>();
                    temp.Add(portNumber,0);
                    distances.Add(portNumber, temp);
                }
                distUnlock();
                prefLock();
                if (prefNeigh.ContainsKey(portNumber))
                    prefNeigh[portNumber] = portNumber;
                else prefNeigh.Add(portNumber, portNumber);
                prefUnlock();
                //hasChanged = true;
            }
            else if (FindNeighbor(portNumber) != null)
            {
#if DEBUG
                Console.WriteLine("--rec {0} is neighbor", portNumber);
#endif
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
#if DEBUG
                Console.WriteLine("--rec {0} isn't neigbor", portNumber);
#endif
                bool dcontain = false;
                distLock();
                foreach (var kvp in distances)  //is portNumber already noted as a possible target from anywhere?
                {
                    if (kvp.Value.ContainsKey(portNumber))
                    {
                        Console.WriteLine("--rec targeted? {0}, {1}", kvp.Key, kvp.Value[kvp.Key]);
                        dcontain = true;
                        break;
                    }
                }

                if (dcontain)
                {
#if DEBUG
                    Console.WriteLine("--rec {0} known as possible target", portNumber);
#endif
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
#if DEBUG
                            Console.WriteLine("--rec {0} unreachable", portNumber);
#endif
                            RemoveNeighbor(portNumber);
                            distUnlock();
                            hasChanged = true;
                        }
                        else
                        {
                            distUnlock();
#if DEBUG
                            Console.WriteLine("--rec No MinDist");
#endif
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
                        prefNeigh.Add(portNumber, portNumber);
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
#if DEBUG
            Console.WriteLine("--minDist by {0}: target {1}", PortNumber, targetNr);
#endif
            if (Globals.ContainsKey(targetNr))
                return new KeyValuePair<short, int>(targetNr, 0);
            KeyValuePair<short, int> result = new KeyValuePair<short, int>(-1, int.MaxValue);
            foreach (KeyValuePair<short, Dictionary<short, int>> node1 in dic1)
                if (FindNeighbor(node1.Key) != null)
                {   //if the first of the connection tuples is a neighbor
                    foreach (KeyValuePair<short, int> node2 in node1.Value)
                    {   //get the distance to the target
#if DEBUG
                        Console.WriteLine("-minD {0}, {1}, {2}", node1.Key, node2.Key, node2.Value);
#endif
                        if (node2.Key == targetNr && node2.Value < result.Value)
                            result = new KeyValuePair<short, int>(node1.Key, node2.Value);
                    }
                }
#if DEBUG
            Console.WriteLine("--minDist output: {0}, {1}", result.Key, result.Value);
#endif
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

        internal short getPreferredNeighbor(short port) {
            prefLock();
            if (!prefNeigh.ContainsKey(port)) Update(port);
            var result = prefNeigh[port];
            prefUnlock();
            return result;
        }
    }
    
}
