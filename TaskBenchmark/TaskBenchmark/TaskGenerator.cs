using System.Buffers;
using System.Diagnostics;

namespace TaskBenchmark;

public static class TaskGenerator
{

    public static async Task<int> IoBoundTask()
    {
        int c = 0;
        Stopwatch stopwatch = new();
        stopwatch.Start();
        while (stopwatch.ElapsedMilliseconds < 200)
        {
            c++;
            await Task.Yield();
            await Task.Delay(1);
        }
        stopwatch.Stop();
        return c;
    }

    public static Task<int> CpuBoundTask()
    {
        int c = 0;
        Stopwatch stopwatch = new();
        stopwatch.Start();
        while (stopwatch.ElapsedMilliseconds < 200)
        {
            c++;
        }
        stopwatch.Stop();
        return Task.FromResult(c);
    }
}
