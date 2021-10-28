using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace MX.Lockbox.UnitTests {
    public class CancellationTests {
        private NamedMutexNamespace NamedMutex { get; } = new NamedMutexNamespace();

        [Fact]
        public void BasicCancellation() {
            //arrange
            var mutexName = "foo";
            using var otherThread = NamedMutex.Obtain(mutexName);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            //act
            Exception error = null;
            try {
                using var mutex = NamedMutex.Obtain(mutexName, TimeSpan.FromMilliseconds(1000), cts.Token);
            } catch (Exception e) {
                error = e;
            }

            //assert
            error.Should().NotBeNull().And.BeOfType(typeof(OperationCanceledException));
        }


        [Fact]
        public async void AsyncCancellation() {
            //arrange
            var mutexName = "foo";
            using var otherThread = await NamedMutex.ObtainAsync(mutexName);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            //act
            Exception error = null;
            try {
                using var mutex = await NamedMutex.ObtainAsync(mutexName, TimeSpan.FromMilliseconds(1000), cts.Token);
            } catch (Exception e) {
                error = e;
            }

            //assert
            error.Should().NotBeNull().And.BeOfType(typeof(OperationCanceledException));
        }

        [Fact]
        public void AlreadyCancelledButOtherwiseCanBeLocked() {
            //arrange
            var mutexName = "foo";            
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            //act
            Exception error = null;
            try {
                using var mutex = NamedMutex.Obtain(mutexName, TimeSpan.FromMilliseconds(1000), cts.Token);
            } catch (Exception e) {
                error = e;
            }

            //assert
            error.Should().NotBeNull().And.BeOfType(typeof(OperationCanceledException));
        }
    }
}
