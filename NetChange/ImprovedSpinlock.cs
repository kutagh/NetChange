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
        public void Lock() {
            if (locker.IsHeldByCurrentThread) {
                counter++;
                return;
            }
            var temp = false;
            while (!temp) {
                //Console.WriteLine("{0} is attempting to lock", name);
                locker.Enter(ref temp);
                //Console.WriteLine("{1} {0} the lock", temp ? "acquired" : "did not acquire", name);
            }
            counter = 0;
        }

        public void Unlock() {
            if (locker.IsHeldByCurrentThread && counter > 0)
                counter--;
            else
                locker.Exit();
        }

        public int DropLock() {
            //if (!locker.IsHeldByCurrentThread) return 0;
            var result = counter;
            counter = 0;
            Unlock();
            return result;
        }

        public void RestoreLock(int c) {
            //if (c == 0) return;
            Lock();
            counter = c;
        }
    }
}
