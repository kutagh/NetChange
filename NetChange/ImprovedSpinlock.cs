using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace NetChange {
    class ImprovedSpinlock {
        internal SpinLock locker = new SpinLock();
        int counter = 0;
        string name;
        public ImprovedSpinlock(string name) {
            this.name = name;
        }
        public void Lock(bool mayLayer = true) {
            if (mayLayer && locker.IsHeldByCurrentThread) {
                Interlocked.Increment(ref counter);
                return;
            }
            else if (mayLayer && locker.IsHeld) {
                Console.WriteLine("Lock {0} is already being held", name);
            }
            var temp = false;
            while (!temp) {
                //Console.WriteLine("{0} is attempting to lock", name);
                locker.Enter(ref temp);
                Console.WriteLine("{1} {0} the lock", temp ? "acquired" : "did not acquire", name);
            }
            counter = 0;
        }

        public void Unlock() {
            if (locker.IsHeldByCurrentThread && counter > 0)
                Interlocked.Decrement(ref counter);
            else
                locker.Exit();
        }
    }
}
