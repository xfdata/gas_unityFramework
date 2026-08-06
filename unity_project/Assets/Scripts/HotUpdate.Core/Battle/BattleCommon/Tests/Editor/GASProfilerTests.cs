using GAS;
using NUnit.Framework;

namespace BattleCommon.Tests
{
    [TestFixture]
    public class GASProfilerTests
    {
        [TearDown]
        public void TearDown()
        {
            GASProfiler.SetBackend(null);
        }

        [Test]
        public void Sample_UsesConfiguredBackend_AndDisposesOnlyOnce()
        {
            var backend = new RecordingProfiler();
            GASProfiler.SetBackend(backend);

            var scope = GASProfiler.Sample("GAS.Tests.Sample");
            scope.Dispose();
            scope.Dispose();

            Assert.AreEqual(1, backend.BeginCount);
            Assert.AreEqual(1, backend.EndCount);
            Assert.AreEqual("GAS.Tests.Sample", backend.LastSampleName);
        }

        [Test]
        public void Sample_WithDefaultBackend_DoesNotThrow()
        {
            GASProfiler.SetBackend(null);

            using (GASProfiler.Sample("GAS.Tests.NoOp"))
            {
            }
        }

        private sealed class RecordingProfiler : IGASProfiler
        {
            public int BeginCount { get; private set; }
            public int EndCount { get; private set; }
            public string LastSampleName { get; private set; }

            public void BeginSample(string name)
            {
                BeginCount++;
                LastSampleName = name;
            }

            public void EndSample()
            {
                EndCount++;
            }
        }
    }
}
