using System;
using System.Collections.Generic;
using System.Text;

namespace MX.Lockbox {
    public interface INamedMutex : IDisposable {
        string Name { get; }
        bool Disposed { get; }
    }
}
