using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace NetChange {
    class ImprovedSpinlock {
        internal SpinLock locker = new SpinLock();
        int counter = 0;
        public void Lock() {
            if (locker.IsHeldByCurrentThread) {
                counter++;
                return;
            }
            var temp = false;
            while (!temp)
                locker.Enter(ref temp);
            counter = 0;
        }

        public void Unlock() {
            if (locker.IsHeldByCurrentThread && counter > 0)
                counter--;
            else
                locker.Exit();
        }
    }
}
