using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace MX.Lockbox.UnitTests {
    public class AsynchronousTests : IClassFixture<TestFixture> {

        private NamedMutexNamespace NamedMutex { get; } = new NamedMutexNamespace(StringComparer.Ordinal);

        private class BackgroundThread :  IDisposable {
            private NamedMutexNamespace NamedMutex { get; }
            private INamedMutex currentLock;
            public BackgroundThread(NamedMutexNamespace ns, string mutexName) {
                this.NamedMutex = ns;
                MutexName = mutexName;            
            }

            public async Task SetLockedAsync() {                
                if (currentLock == null) {
                    currentLock = await NamedMutex.ObtainAsync(MutexName);
                }
            }

            public void SetUnlocked() {
                currentLock?.Dispose();
                currentLock = null;
            }

            public string MutexName { get; }
            public void Dispose() {
                SetUnlocked();
            }
        }

        [Fact]
        public async void TestSameLockName() {
            //arrange
            string mutexName = "foo";
            using var backgroundThread = new BackgroundThread(NamedMutex, mutexName);
            await backgroundThread.SetLockedAsync();

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

        [Fact]
        public async void TestDifferentLock() {
            //arrange
            string mutexName = "foo";
            string otherMutexName = "bar";
            using var backgroundThread = new BackgroundThread(NamedMutex, otherMutexName);
            await backgroundThread.SetLockedAsync();


            //act
            bool obtained = false;
            try {
                using (var mutex = await NamedMutex.ObtainAsync(mutexName, 100)) {
                    obtained = true;
                }
            } catch (TimeoutException) {
                obtained = false;
            }

            //assert
            obtained.Should().BeTrue(because: "the other thread was holding a different mutex");
        }

        [Fact]
        public async void TestLettingLockGo() {
            //arrange
            string mutexName = "foo";
            using var backgroundThread = new BackgroundThread(NamedMutex, mutexName);
            await backgroundThread.SetLockedAsync();

            bool backgroundThreadIsLocked = true;

            ThreadPool.QueueUserWorkItem(state => {
                Thread.Sleep(50);
                backgroundThreadIsLocked = false;
                backgroundThread.SetUnlocked();                
            });

            //act
            bool obtainedWhileLocked;
            using (var mutex = await NamedMutex.ObtainAsync(mutexName, 10000)) {
                obtainedWhileLocked = backgroundThreadIsLocked;
            }
            
            //assert
            obtainedWhileLocked.Should().BeFalse(because: "the other thread was holding the lock");
        }

        [Fact]
        public async void Test80Locks() {
            //arrange
            int threadCount = 80;
            BackgroundThread[] backgroundThreads = new BackgroundThread[threadCount];
            try {
                for (int i = 0; i < threadCount; ++i) {
                    backgroundThreads[i] = new BackgroundThread(NamedMutex, $"mutex.{i}");
                }

                //act 
                for (int i = 0; i < threadCount; ++i) {
                    await backgroundThreads[i].SetLockedAsync();                
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
        public async void TestSameLockNameOneSynchronous() {
            //arrange
            string mutexName = "foo";
            using var backgroundThread = new BackgroundThread(NamedMutex, mutexName);
            await backgroundThread.SetLockedAsync();

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
    }
}
