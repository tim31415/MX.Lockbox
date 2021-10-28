using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MX.Lockbox.UnitTests {
    public class TestFixture {
        public TestFixture() {
            ThreadPool.SetMinThreads(100, 100);
        }
    }
}