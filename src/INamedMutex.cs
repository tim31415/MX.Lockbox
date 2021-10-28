using System;
using System.Collections.Generic;
using System.Text;

namespace MX.Lockbox {
    /// <summary>
    /// Represents a named mutex
    /// </summary>
    public interface INamedMutex : IDisposable {
        /// <summary>
        /// Name of the mutex obtained
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether or not this mutex has been disposed (released)
        /// </summary>
        bool Disposed { get; }
    }
}
