using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetChange {
    class NetwProg
    {
        //connected => all connected neighbors
        static Dictionary<short, int> distances = new Dictionary<short,int>();     //list of known/connected nodes and distances to the others from there
        static Dictionary<short, short> prefNeigh = new Dictionary<short,short>(); //to whom to send messages when it has to go to some node

        public void InitNetChange()
        {
            foreach(KeyValuePair<short, Client> neighbor in connected)
            {
                distances.Add(neighbor.Key, short.MaxValue);
            }
        }

        public void Update(short Nid, Dictionary<short, int> Ndata)
        {

        }
    }

    
}