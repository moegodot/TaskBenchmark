using System.Buffers;
using System.Diagnostics;

namespace TaskBenchmark;

public static class TaskGenerator
{

    public static async Task IoBoundTask()
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        while (stopwatch.ElapsedMilliseconds < 200)
        {
            await Task.Yield();
            await Task.Delay(1);
        }
        stopwatch.Stop();
    }

    public static Task CpuBoundTask()
    {
        SpinWait spinWait = new();
        Stopwatch stopwatch = new();
        stopwatch.Start();
        while (stopwatch.ElapsedMilliseconds < 200)
        {
            spinWait.SpinOnce(int.MaxValue);
        }
        stopwatch.Stop();
        return Task.CompletedTask;
    }
}
