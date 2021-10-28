using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace MX.Lockbox.UnitTests {
    public class ContentionTests : IClassFixture<TestFixture> {
        private NamedMutexNamespace NamedMutex { get; } = new NamedMutexNamespace();

        private class HavocTester {
            public volatile int value;

            public void RunTask() {
                int oldValue = value;
                Thread.Sleep(3);
                value = oldValue + 1;
            }
        }

        [Fact]
        public void WithoutLocksCausesHavoc() {
            //arrange
            var havocTester = new HavocTester();
            var iterations = 100;

            //act
            Parallel.For(0, iterations, i => havocTester.RunTask());

            //assert
            havocTester.value.Should().BeLessThan(iterations, because: $"we expect {iterations} in parallel without locking to mangle the value");
        }

        [Fact]
        public void WithLocksWorksCorrectly() {
            //arrange
            var havocTester = new HavocTester();
            var iterations = 100;

            //act
            Parallel.For(0, iterations, i => {
                using (NamedMutex.Obtain("foo")) {
                    havocTester.RunTask();
                }
            });

            //assert
            havocTester.value.Should().Be(iterations, because: $"we expect lock primitives to solve this problem");
        }

        [Fact]
        public void WithDifferentNamedLocksCausesHavoc() {
            //arrange
            var havocTester = new HavocTester();
            var iterations = 100;

            //act
            Parallel.For(0, iterations, i => {
                using (NamedMutex.Obtain($"foo.{i}")) {
                    havocTester.RunTask();
                }
            });

            //assert
            havocTester.value.Should().BeLessThan(iterations, because: $"we expect {iterations} in parallel without locking to mangle the value");
        }

        [Fact]
        public void WithParallelLocksWorksCorrectly() {
            //arrange
            int groupCount = 10;
            HavocTester[] havocTesters = new HavocTester[groupCount];
            for (int i = 0; i < groupCount; ++i)
                havocTesters[i] = new HavocTester();

            var iterations = 100;

            //act
            Parallel.For(0, iterations * groupCount, i => {
                int groupIdx = i % groupCount;
                using (NamedMutex.Obtain($"foo.{groupIdx}")) {
                    havocTesters[groupIdx].RunTask();
                }
            });

            //assert
            using var scope = new FluentAssertions.Execution.AssertionScope();

            for (int i = 0; i < groupCount; ++i)
                havocTesters[i].value.Should().Be(iterations, because: $"we expect lock primitives to solve this problem");

            NamedMutex.InstanceCount.Should().Be(0);
            NamedMutex.SemaphoresCreated.Should().Be(NamedMutex.SemaphoresDisposed);
        }
    }
}