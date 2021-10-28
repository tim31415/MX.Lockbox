using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MX.Lockbox {
    /// <summary>
    /// Represents a namespace and factory for named mutexes
    /// </summary>
    public sealed partial class NamedMutexNamespace {
        /// <summary>
        /// Represents a global namespace for named mutexes
        /// </summary>
        public static NamedMutexNamespace Global { get; } = new NamedMutexNamespace(StringComparer.Ordinal);

        /// <summary>
        /// Constructs a new namespace for mutexes using <see cref="StringComparer.Ordinal"/> string comparison
        /// </summary>
        public NamedMutexNamespace()
            : this(StringComparer.Ordinal) {
        }

        /// <summary>
        /// Constructs a new namespace for named mutexes using the specific <see cref="StringComparer"/> for comparing mutex names
        /// </summary>
        /// <param name="stringComparison"></param>
        public NamedMutexNamespace(StringComparer stringComparison) {
            instances = new Dictionary<string, Entry>(stringComparison);
        }

        private Dictionary<string, Entry> instances;


        /// <summary>
        /// Asynchronously obtains a named lock
        /// </summary>
        /// <param name="name">name of lock</param>
        /// <param name="timeoutMs">The number of milliseconds to wait, <see cref="Timeout.Infinite"/> (-1) to wait indefinitely, or zero to test the state of the lock and return immediately.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe</param>
        /// <returns>a <see cref="Mutex" /> instance if successful</returns>
        /// <exception cref="TimeoutException">a lock could not be obtained within the specified <paramref name="timeoutMs"/></exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutMs"/> is a number other than -1, which represents an infinite timeout.<br />-or-<br /><paramref name="timeoutMs"/> is greater than <see cref="int.MaxValue"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null</exception>
        public async Task<INamedMutex> ObtainAsync(string name, int timeoutMs, CancellationToken cancellationToken = default) {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            cancellationToken.ThrowIfCancellationRequested();

            Entry entry;
            lock (instances) {
                if (instances.TryGetValue(name, out entry)) {
                    entry.refCount++;
                } else {
                    entry = new Entry();
                    Interlocked.Increment(ref semaphoresCreated);

                    try {
                        instances.Add(name, entry);
                        return new Mutex(this, name, entry);
                    } catch {
                        entry.mutex.Dispose();
                        Interlocked.Increment(ref semaphoresDisposed);
                        throw;
                    }
                }
            }

            try {
                bool obtained = await entry.mutex.WaitAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
                if (obtained) {
                    return new Mutex(this, name, entry);
                }
                throw new TimeoutException($"timeout expired waiting to obtain {nameof(Mutex)} named {name}");
            } catch {
                lock (instances) {
                    entry.refCount--;
                    if (entry.refCount == 0) {
                        //in the unlikely event that the lock gets released after we've given up
                        entry.mutex.Dispose();
                        Interlocked.Decrement(ref semaphoresDisposed);
                        instances.Remove(name);
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Asynchronously attempts to obtain a named lock while observing a <see cref="CancellationToken"/>
        /// </summary>
        /// <param name="name">name of the mutex</param>
        /// <param name="timeout">The amount of time to wait to wait or <see cref="TimeSpan.Zero"/> to test the state of the lock and return immediately.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe</param>
        /// <returns>a <see cref="Mutex" /> instance if successful</returns>
        /// <exception cref="TimeoutException">a lock could not be obtained within the specified <paramref name="timeout"/></exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is a value other than -1ms, which represents an infinite timeout.<br />-or-<br /><paramref name="timeout"/> is greater than <see cref="int.MaxValue"/> ms.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null</exception>
        public Task<INamedMutex> ObtainAsync(string name, TimeSpan timeout, CancellationToken cancellationToken = default) {            
            return ObtainAsync(name, (int)timeout.TotalMilliseconds, cancellationToken);
        }

        /// <summary>
        /// Asynchronously obtains a named lock while observing a <see cref="CancellationToken"/>
        /// </summary>
        /// <param name="name">name of the mutex</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe</param>
        /// <remarks>This overload will wait indefiniately to obtain the lock, unless cancelled</remarks>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null</exception>
        public Task<INamedMutex> ObtainAsync(string name, CancellationToken cancellationToken) {
            return ObtainAsync(name, Timeout.Infinite, cancellationToken);
        }


        /// <summary>
        /// Asynchronously obtains a named lock
        /// </summary>
        /// <param name="name">name of the mutex</param>
        /// <remarks>This overload will wait indefiniately to obtain the lock</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null</exception>
        public Task<INamedMutex> ObtainAsync(string name) {
            return ObtainAsync(name, Timeout.Infinite, CancellationToken.None);
        }

        /// <summary>
        /// Synchronously attempt to obtain a named lock
        /// </summary>
        /// <param name="name">name of lock</param>
        /// <param name="timeoutMs">The number of milliseconds to wait, <see cref="Timeout.Infinite"/> (-1) to wait indefinitely, or zero to test the state of the lock and return immediately.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe</param>
        /// <returns>a <see cref="Mutex" /> instance if successful</returns>
        /// <exception cref="TimeoutException">a lock could not be obtained within the specified <paramref name="timeoutMs"/></exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutMs"/> is a number other than -1, which represents an infinite timeout.<br />-or-<br /><paramref name="timeoutMs"/> is greater than <see cref="int.MaxValue"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null</exception>
        public INamedMutex Obtain(string name, int timeoutMs, CancellationToken cancellationToken = default) {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            cancellationToken.ThrowIfCancellationRequested();

            Entry entry;
            lock (instances) {
                if (instances.TryGetValue(name, out entry)) {
                    entry.refCount++;
                } else {
                    entry = new Entry();
                    Interlocked.Increment(ref semaphoresCreated);
                    try {
                        instances.Add(name, entry);
                        return new Mutex(this, name, entry);
                    } catch {
                        entry.mutex.Dispose();
                        Interlocked.Decrement(ref semaphoresDisposed);
                        throw;
                    }
                }
            }

            try {
                bool obtained = entry.mutex.Wait(timeoutMs, cancellationToken);
                if (obtained) {
                    return new Mutex(this, name, entry);
                }
                throw new TimeoutException($"timeout expired waiting to obtain {nameof(Mutex)} named {name}");
            } catch {
                lock (instances) {
                    entry.refCount--;
                    if (entry.refCount == 0) {
                        //in the unlikely event that the lock gets released after we've given up
                        entry.mutex.Dispose();
                        Interlocked.Decrement(ref semaphoresDisposed);
                        instances.Remove(name);
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Attempt to obtain a named lock while observing a <see cref="CancellationToken"/>
        /// </summary>
        /// <param name="name">name of the mutex</param>
        /// <param name="timeout">The amount of time to wait to wait or <see cref="TimeSpan.Zero"/> to test the state of the lock and return immediately.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe</param>
        /// <returns>a <see cref="Mutex" /> instance if successful</returns>
        /// <exception cref="TimeoutException">a lock could not be obtained within the specified <paramref name="timeout"/></exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is a value other than -1ms, which represents an infinite timeout.<br />-or-<br /><paramref name="timeout"/> is greater than <see cref="int.MaxValue"/> ms.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null</exception>
        public INamedMutex Obtain(string name, TimeSpan timeout, CancellationToken cancellationToken = default) {
            return Obtain(name, (int)timeout.TotalMilliseconds, cancellationToken);
        }

        /// <summary>
        /// Obtains a named lock while observing a <see cref="CancellationToken"/>
        /// </summary>
        /// <param name="name">name of the mutex</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe</param>
        /// <remarks>This overload will wait indefiniately to obtain the lock, unless cancelled</remarks>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null</exception>
        public INamedMutex Obtain(string name, CancellationToken cancellationToken) {
            return Obtain(name, Timeout.Infinite, cancellationToken);
        }

        /// <summary>
        /// Obtains a named lock
        /// </summary>
        /// <param name="name">name of the mutex</param>
        /// <remarks>This overload will wait indefiniately to obtain the lock</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null</exception>
        public INamedMutex Obtain(string name) {
            return Obtain(name, Timeout.Infinite, CancellationToken.None);
        }


        private class Entry {
            public SemaphoreSlim mutex = new SemaphoreSlim(0, 1);
            public int refCount = 1;
        }


        /// <summary>
        /// For diagnostic purposes, returns the number of mutexes being tracked by this namespace
        /// </summary>
        public int InstanceCount {
            get {
                lock (instances) {
                    return instances.Count;
                }
            }
        }

        private int semaphoresCreated = 0, semaphoresDisposed = 0;

        /// <summary>
        /// For diagnostic purposes, returns the number of semaphores created by this namespace
        /// </summary>
        public int SemaphoresCreated => semaphoresCreated;

        /// <summary>
        /// For diagnostic purposes, returns the number of semaphores disposed by this namespace
        /// </summary>
        public int SemaphoresDisposed => semaphoresDisposed;

        private class Mutex : INamedMutex {
            public string Name { get; }
            private readonly Entry entry;
            private int disposedCount = 0;
            private readonly NamedMutexNamespace ns;

            public Mutex(NamedMutexNamespace ns, string name, Entry entry) {
                this.ns = ns;
                Name = name;
                this.entry = entry;
            }

            public void Dispose() {
                //prevent disposing more than once
                if (Interlocked.Increment(ref disposedCount) != 1)
                    return;

                entry.mutex.Release();

                lock (ns.instances) {
                    if (entry != null) {
                        lock (ns.instances) {
                            entry.refCount--;
                            if (entry.refCount == 0) {
                                ns.instances.Remove(Name);
                                entry.mutex.Dispose();
                                Interlocked.Increment(ref ns.semaphoresDisposed);
                            }
                        }
                    }
                }
            }

            public bool Disposed => disposedCount > 0;
        }
    }
}