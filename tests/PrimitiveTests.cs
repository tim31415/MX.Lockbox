using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace MX.Lockbox.UnitTests {
    public class PrimitiveTests : IClassFixture<TestFixture> {

        private NamedMutexNamespace NamedMutex { get; } = new NamedMutexNamespace(StringComparer.Ordinal);

        private class BackgroundThread :  IDisposable {
            private readonly object sync = new object();
            public enum State {
                Unlocked,
                Locked,
                Disposed
            }
            private State desiredState = State.Unlocked;
            private State currentState = State.Unlocked;
            private NamedMutexNamespace NamedMutex { get; } 
            public BackgroundThread(NamedMutexNamespace ns, string mutexName) {
                this.NamedMutex = ns;
                MutexName = mutexName;
                ThreadPool.QueueUserWorkItem(ThreadProc);
            }

            public void SetLocked() {
                lock (sync) {
                    if (currentState == State.Disposed || desiredState == State.Disposed)
                        throw new ObjectDisposedException(GetType().Name);

                    desiredState = State.Locked;
                    Monitor.PulseAll(sync);

                    while (currentState != State.Locked) {
                        Monitor.Wait(sync);
                    }
                }
            }

            public void SetUnlocked() {
                lock (sync) {
                    if (currentState == State.Disposed || desiredState == State.Disposed)
                        throw new ObjectDisposedException(GetType().Name);

                    desiredState = State.Unlocked;
                    Monitor.PulseAll(sync);

                    while (currentState != State.Unlocked) {
                        Monitor.Wait(sync);
                    }
                }
            }

            public string MutexName { get; }

            public void Dispose() {
                lock (sync) {
                    desiredState = State.Disposed;
                    Monitor.PulseAll(sync);
                }
            }

            private void ThreadProc(object _) {
                lock (sync) {
                    while (desiredState != State.Disposed) {                        
                        if (desiredState == currentState) {
                            Monitor.Wait(sync);
                        } else {
                            if (desiredState == State.Locked) {
                                using (var mutex = NamedMutex.Obtain(MutexName)) {
                                    currentState = State.Locked;
                                    Monitor.PulseAll(sync);

                                    while (desiredState == State.Locked) {
                                        Monitor.Wait(sync);
                                    }
                                }
                                currentState = State.Unlocked;
                                Monitor.PulseAll(sync);
                            } else if (currentState != State.Unlocked) {
                                currentState = State.Unlocked;
                                Monitor.PulseAll(sync);
                            }
                        }
                    }
                    currentState = State.Disposed;
                    Monitor.PulseAll(sync);
                }
            }
        }

        [Fact]
        public void TestSameLockName() {
            //arrange
            string mutexName = "foo";
            using var backgroundThread = new BackgroundThread(NamedMutex, mutexName);
            backgroundThread.SetLocked();

            //act
            bool obtained = false;
            try {
                using (var mutex = NamedMutex.Obtain(mutexName, 0)) {
                    obtained = true;
                }
            } catch (TimeoutException) {
                obtained = false;
            }

            //assert
            obtained.Should().BeFalse(because: "the other thread was holding the mutex");
        }

        [Fact]
        public void TestDifferentLock() {
            //arrange
            string mutexName = "foo";
            string otherMutexName = "bar";
            using var backgroundThread = new BackgroundThread(NamedMutex, otherMutexName);
            backgroundThread.SetLocked();


            //act
            bool obtained = false;
            try {
                using (var mutex = NamedMutex.Obtain(mutexName, 100)) {
                    obtained = true;
                }
            } catch (TimeoutException) {
                obtained = false;
            }

            //assert
            obtained.Should().BeTrue(because: "the other thread was holding a different mutex");
        }

        [Fact]
        public void TestLettingLockGo() {
            //arrange
            string mutexName = "foo";
            using var backgroundThread = new BackgroundThread(NamedMutex, mutexName);
            backgroundThread.SetLocked();

            bool backgroundThreadIsLocked = true;

            ThreadPool.QueueUserWorkItem(state => {
                Thread.Sleep(50);
                backgroundThreadIsLocked = false;
                backgroundThread.SetUnlocked();                
            });

            //act
            bool obtainedWhileLocked;
            using (var mutex = NamedMutex.Obtain(mutexName, 10000)) {
                obtainedWhileLocked = backgroundThreadIsLocked;
            }
            
            //assert
            obtainedWhileLocked.Should().BeFalse(because: "the other thread was holding the lock");
        }

        [Fact]
        public void Test80Locks() {
            //arrange
            int threadCount = 80;
            //ThreadPool.SetMaxThreads(1000, 1000);
            BackgroundThread[] backgroundThreads = new BackgroundThread[threadCount];
            try {
                for (int i = 0; i < threadCount; ++i) {
                    backgroundThreads[i] = new BackgroundThread(NamedMutex, $"mutex.{i}");
                }

                //act 
                for (int i = 0; i < threadCount; ++i) {
                    backgroundThreads[i].SetLocked();                
                }
                int numberOfInstancesWhileLocked = NamedMutex.InstanceCount;

                for (int i = 0; i < threadCount; ++i) {
                    backgroundThreads[i].SetUnlocked();
                }
                int numberOfInstancesWhileUnlocked = NamedMutex.InstanceCount;


                //assert
                using var scope = new AssertionScope();
                numberOfInstancesWhileLocked.Should().Be(threadCount);
                numberOfInstancesWhileUnlocked.Should().Be(0);
                NamedMutex.SemaphoresCreated.Should().Be(threadCount);
                NamedMutex.SemaphoresDisposed.Should().Be(threadCount);
            } finally {
                for (int i = 0; i < threadCount; ++i) {
                    backgroundThreads[i]?.Dispose();
                }
            }
        }


        [Fact]
        public async void TestSameLockNameOneAsync() {
            //arrange
            string mutexName = "foo";
            using var backgroundThread = new BackgroundThread(NamedMutex, mutexName);
            backgroundThread.SetLocked();

            //act
            bool obtained = false;
            try {
                using (var mutex = await NamedMutex.ObtainAsync(mutexName, 0)) {
                    obtained = true;
                }
            } catch (TimeoutException) {
                obtained = false;
            }

            //assert
            obtained.Should().BeFalse(because: "the other thread was holding the mutex");
        }

    }
}
