﻿/* Ian Zunderdorp (3643034) & Bas Brouwer (3966747)
 * 
 * Before compiling the program, first set the solution configuration to Release. See:
 * Voordat je de programma compileert, zet eerst de solution configuration op Release. Zie daarvoor:
 * (text) http://msdn.microsoft.com/en-us/library/vstudio/bb166577%28v=vs.100%29.aspx
 * (visual) http://www2.warwick.ac.uk/fac/sci/physics/staff/research/bmorgan/geant4/installingonwindows/step_10_set_build_configuration.png
 * 
 */


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetChange {
    internal static class Globals {
        private static Dictionary<short, Client> connected = new Dictionary<short, Client>();
        public static long TotalDistanceEstimatesSent { get; private set; }
        public static bool PrintStatusChanges = false;

        private static ImprovedSpinlock connectedLocker = new ImprovedSpinlock("connLocker"), distEstLocker = new ImprovedSpinlock("estLocker");
        
        static Globals() {
            TotalDistanceEstimatesSent = 0;
        }

        public static void Lock() {
            connectedLocker.Lock();
        }

        public static void Unlock() {
            connectedLocker.Unlock();
        }

        public static void Remove(short p) {
            Lock();
            connected.Remove(p);
            Unlock();
        }

        public static void IncrementTotalDistanceEstimatesSent(int n = 1) {
            distEstLocker.Lock();
            TotalDistanceEstimatesSent += n;
            distEstLocker.Unlock();
        }

        public static void Add(short p, Client c) {
            Lock();
            connected.Add(p, c);
            Unlock();
        }

        public static Client Get(short p) {
            Lock();
            Client result = connected.ContainsKey(p) ? connected[p] : null;
            Unlock();
            return result;
        }

        public static void Set(short p, Client c) {
            Lock();
            if (connected.ContainsKey(p))
                connected[p] = c;
            else
                connected.Add(p, c);
            Unlock();
        }

        public static bool ContainsKey(short p) {
            Lock();
            var result = connected.ContainsKey(p);
            Unlock();
            return result;
        }

        /// <summary>
        /// Only call if you actually have a lock
        /// </summary>
        /// <returns>Null if no lock was acquired prior to the method being called, the clients dictionary if the lock was acquired</returns>
        public static Dictionary<short, Client> GetDictionary() {
            if (connectedLocker.locker.IsHeldByCurrentThread)
                return connected;
            return null;
        }

        static char separator = '|';
        
        public static string CreatePackage(string packageName, short destination, string payload) {
            return string.Format("{0}{3}{1}{3}{2}", packageName, destination, payload, separator);
        }

        public static string[] UnpackPackage(string package) {
            if (package.Count(x => x == '|') == 2)
                return package.Split(separator);
            else return new string[] { package };
        }
    }

    class NetwProg {
        #region Console Window code
        const int SWP_NOSIZE = 0x0001; //Ignores the resize parameters when calling SetWindowPos.
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        private static IntPtr MyConsole = GetConsoleWindow();

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);
        #endregion
        
        static NetChangeNode node;
        static Server server;
        static int SlowDown = 0;
        static short myPortNumber;
        static string parameterError = "The {0} parameter '{1}' was not correct, please enter {2}.";

        static void Main(string[] args) {
#if DEBUG
            Console.WriteLine("Debugging mode");
#endif
            if (args.Length == 0) {
                args = new string[] { "1000", "1001" };
            }
            int iterator = 0;
            if (args[0][0] == 'p') {
                // Set console position
                SetWindowPos(MyConsole, 0, int.Parse(args[0].Substring(1)), int.Parse(args[1].Substring(1)), 0, 0, SWP_NOSIZE);
                iterator += 2;
                Console.Title = string.Format("port = {0}, x = {1}, y = {2}", args[iterator], args[0].Substring(1), args[1].Substring(1)); // Port number
            }
            else {
                Console.Title = string.Format("port = {0}", args[iterator]); // Port number
            }
            myPortNumber = short.Parse(args[iterator]);
            var list = new List<short>();
            while (++iterator < args.Length) // All neighbors
                list.Add(short.Parse(args[iterator]));

            // Set up local graph
            node = new NetChangeNode(myPortNumber);
            foreach (var port in list.Where(x => x > myPortNumber)) node.AddNeighbor(port);
            
            server = new Server(myPortNumber);
            // Create listener
            Thread th = new Thread(new ThreadStart(Listen));
            th.Start();
            
            var connectWith = list.Where(x => x > myPortNumber); // Filter the neighbors with a lower port number
            foreach (var port in connectWith) {
                bool retry = true;
                int attempt = 0;
                while (retry) {
                    try {
                        var client = new Client(myPortNumber, port);
                        Globals.Add(port, client);
                        retry = false;
#if DEBUG
                        Console.WriteLine(client.CreateHandshake(myPortNumber));
#endif
                    }
                    catch {
#if DEBUG
                        Console.WriteLine("Failed to connect, retrying for {0}th time", attempt++);
#endif
                        System.Threading.Thread.Sleep(3);
                        if (++attempt > 1000) {
                            Console.WriteLine("Did not manage to connect, aborting");
                            retry = false;
                        }
                    }
                }
            }
            
            Globals.Lock();
            foreach (var client in Globals.GetDictionary()) {
#if DEBUG
                Console.WriteLine("Have{0} connected to {1}", client.Value.IsConnected ? "" : "n't", client.Key);
#endif
                var c = client.Value;
                Thread listener = new Thread(new ThreadStart(() => ListenForMessages(c)));
                listener.Start();
                //Task.Factory.StartNew(() => ListenForMessages(c));
            }
            Globals.Unlock();
            node.Updating = true;
            node.Update();
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
#if DEBUG
            Globals.Lock();
            foreach (var client in Globals.GetDictionary()) { client.Value.SendMessage("Test"); Console.WriteLine("Sent message to {0}", client.Key); }
            Globals.Unlock();
            //Console.ReadLine();
#endif
            while (true) {
                var input = Console.ReadLine();

#if DEBUG
                if (input.StartsWith("F")) {
                    var port = short.Parse(input.Substring(2));
                    Console.WriteLine("FindNeighbor returned {1} for port {0}", port, node.FindNeighbor(port) == null ? "null" : "a NetChangeNode");
                    continue;
                }

                if (input.StartsWith("P")) {
                    var port = short.Parse(input.Substring(2));
                    Console.WriteLine("getPreferredNeighbor returned {1} for port {0}", port, node.getPreferredNeighbor(port));
                    continue;
                }
#endif

                if (input.StartsWith("S")) {
                    if (int.TryParse(input.Substring(2), out SlowDown) && SlowDown >= 0)
                        continue;
                    Console.WriteLine(parameterError, "slowdown", "n", "a positive number");
                    if (SlowDown < 0) SlowDown = 0;
                    continue;
                }
                if (input.StartsWith("R")) {
                    node.PrintRoutingTable();
                    continue;
                }
                if (input.StartsWith("M")) {
                    Console.WriteLine("Total number of distance estimations sent: {0}", Globals.TotalDistanceEstimatesSent);
                    continue;
                }
                if (input.StartsWith("D")) {
                    short target;
                    if (short.TryParse(input.Substring(2), out target)) {
                        Globals.Lock();
                        if (Globals.GetDictionary().ContainsKey(target))
                        {
                            Globals.Get(target).SendMessage(Globals.CreatePackage("Delete", target, myPortNumber.ToString()));
                            Globals.Remove(target);
                            Globals.Unlock();
                            node.RemoveNeighbor(target);
                            if (Globals.PrintStatusChanges) Console.WriteLine("Verbinding verbroken met node {0}", target);
                        }
                        else
                        {
                            Globals.Unlock();
                            Console.WriteLine("Port {0} is not connected to this process", target);
                        }
                        continue;
                    }
                    Console.WriteLine(parameterError, "delete", "port", "a valid port number");
                    continue;
                }
                if (input.StartsWith("C")) {
                    short port;
                    if (short.TryParse(input.Substring(2), out port)) {
                        var client = new Client(myPortNumber, port);
                        Globals.Add(port, client);
                        Thread listener = new Thread(new ThreadStart(() => ListenForMessages(client)));
                        listener.Start();
                        node.AddNeighbor(port);
                        if(Globals.PrintStatusChanges) Console.WriteLine("Nieuwe verbinding met node {0}", port);
                        continue;
                    }
                    Console.WriteLine(parameterError, "create", "port", "a valid port number");
                }
                if (input.StartsWith("B")) {
                    var split = input.Split(' ');
                    short target;
                    if (split.Length > 2 && short.TryParse(split[1], out target)) {
                        var message = new StringBuilder(split[2]);
                        for (int i = 3; i < split.Length; i++)
                            message.AppendFormat(" {0}", split[i]);
                        var msg = Globals.CreatePackage("Broadcast", target, message.ToString());
                        var sendTo = node.getPreferredNeighbor(target);
#if DEBUG
                        Console.WriteLine("About to broadcast {0} to port {1} via port {2}", msg, target, sendTo);
#endif
                        Globals.Get(sendTo).SendMessage(msg);
#if DEBUG
                        Console.WriteLine("Sent message to {0}", target);
#endif
                        continue;
                    }
                    if (split.Length > 2)
                        Console.WriteLine(parameterError, "broadcast", "port", "a valid port number");
                    else
                        Console.WriteLine(parameterError, "broadcast", "message", "a valid message");
                    continue;
                }
                if (input.StartsWith("T")) {
                    if (input.Substring(2).Equals("on", StringComparison.CurrentCultureIgnoreCase))
                        Globals.PrintStatusChanges = true;
                    else if (input.Substring(2).Equals("off", StringComparison.CurrentCultureIgnoreCase))
                        Globals.PrintStatusChanges = false;
                    else
                        Console.WriteLine(parameterError, "toggle status changes", "status", "'on' or 'off'");
                    continue;
                }

                Console.WriteLine("The command {0} could not be found. Please retry.", input);

            }
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e) {
            Globals.Lock();
            foreach (var c in Globals.GetDictionary())
                c.Value.SendMessage(Globals.CreatePackage("Delete", c.Key, myPortNumber.ToString()));
            Globals.Unlock();

        }

        static void Listen() {
#if DEBUG   
            Console.WriteLine("Listening");
#endif
            var client = server.AcceptConnection() as Client;
#if DEBUG   
            Console.WriteLine("Client connected");
#endif
            var handShake = client.ReadMessage();
#if DEBUG   
            Console.WriteLine("Handshake message: " + handShake);
#endif
            var port = client.ParseHandshake(handShake);
            if (port < 0) { Console.WriteLine("Didn't get a valid handshake. Handshake message: '{0}'", handShake); Listen(); }
            else {
                //node.nbLocker.Lock();
                //node.rtLock();
                //node.Updating = false;
                //node.AddNeighbor(port);
#if DEBUG   
            Console.WriteLine("Adding to list of connected clients");
#endif
                Globals.Add(port, client);
                client.ConnectedTo = port;
#if DEBUG   
            Console.WriteLine("Starting to listen for messages from {0}", port);
#endif
                Thread listener = new Thread(new ThreadStart(() => ListenForMessages(client)));
                listener.Start();
                //node.Updating = true;
                //node.Update();
                //node.nbLocker.Unlock();
                //node.rtUnlock();
                node.AddNeighbor(port);
#if DEBUG
            Console.WriteLine("Accepted connection");
#endif
                Listen();
            }
        }

        static void ListenForMessages(Client c) {
#if DEBUG
            Console.WriteLine("Listening for messages from {0}", c.ConnectedTo);
#endif
            try {
                var message = c.ReadMessage();
#if DEBUG
                Console.WriteLine(message);
#endif
                if (SlowDown > 0)
                    Thread.Sleep(new TimeSpan(0, 0, 0, 0, SlowDown));

                var msg = Globals.UnpackPackage(message);
                if (msg[0] == "Broadcast") {
                    var port = short.Parse(msg[1]);
                    if (port == myPortNumber) Console.WriteLine(msg[2]);
                    else {
                        var sendTo = node.getPreferredNeighbor(port);
                        Globals.Get(sendTo).SendMessage(message);
                    }
                }
                else if (msg[0] == "Delete" && short.Parse(msg[1]) == myPortNumber) {
                    var toDelete = short.Parse(msg[2]);
                    Globals.Lock();
                    if (Globals.ContainsKey(toDelete))
                        Globals.Remove(toDelete);
                    Globals.Unlock();
                    node.RemoveNeighbor(toDelete);
                }
                else node.InterpretMess(message);

                // Handle messages
                ListenForMessages(c);
            }
            catch (IOException) { }
        }
    }
}
