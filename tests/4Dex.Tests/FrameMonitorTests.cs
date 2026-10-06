using FourDex.Player;
using Xunit;

namespace FourDex.Tests
{
    /// <summary>The HUD's frame-cost readout: smoothed enough to read, honest about spikes.</summary>
    public class FrameMonitorTests
    {
        [Fact]
        public void SteadyFramesReadAsTheirOwnRate()
        {
            var monitor = new FrameMonitor();
            for (int i = 0; i < 200; i++)
                monitor.Sample(1f / 90f);

            Assert.Equal(11.1f, monitor.MeanMs, 1);
            Assert.Equal(90f, monitor.Fps, 0);
        }

        [Fact]
        public void ASpikeIsReportedEvenThoughTheMeanAbsorbsIt()
        {
            var monitor = new FrameMonitor(windowSeconds: 1f);
            for (int i = 0; i < 45; i++)
                monitor.Sample(1f / 90f);
            monitor.Sample(0.040f); // one 40 ms frame: felt, but far too brief to move the mean much
            for (int i = 0; i < 90; i++)
                monitor.Sample(1f / 90f);

            Assert.True(monitor.MeanMs < 15f, $"the mean stays readable, was {monitor.MeanMs}");
            Assert.Equal(40f, monitor.WorstMs, 0);
        }

        [Fact]
        public void TheWorstFigureIsRecent_NotAllTime()
        {
            var monitor = new FrameMonitor(windowSeconds: 0.5f);
            monitor.Sample(0.100f);
            for (int i = 0; i < 200; i++) // two full windows of smooth frames
                monitor.Sample(1f / 90f);

            Assert.True(monitor.WorstMs < 20f, $"an old spike stops accusing a recovered frame rate, was {monitor.WorstMs}");
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.PositiveInfinity)]
        public void NonsenseSamplesAreIgnored(float delta)
        {
            var monitor = new FrameMonitor();
            monitor.Sample(1f / 90f);
            float before = monitor.MeanMs;
            monitor.Sample(delta);

            Assert.Equal(before, monitor.MeanMs);
        }
    }
}
