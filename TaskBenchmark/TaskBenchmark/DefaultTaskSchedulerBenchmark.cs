using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace TaskBenchmark;

[SimpleJob(RunStrategy.Monitoring, iterationCount:1)]
public class DefaultTaskSchedulerBenchmark
{
    public Task<Task<int>> Tasks = null!;

    [IterationSetup]
    public void Generate()
    {
        List<Task<Task<int>>> IoBoundTasks = [];
        List<Task<Task<int>>> CpuBoundTasks = [];

        for (var i = 0; i != 128; i++)
        {
            var t = new Task<Task<int>>(async () => await TaskGenerator.IoBoundTask());
            t.ConfigureAwait(false);
            IoBoundTasks.Add(t);
        }
        for (var i = 0; i != 512; i++)
        {
            var t = new Task<Task<int>>(async () => await TaskGenerator.CpuBoundTask());
            t.ConfigureAwait(false);
            CpuBoundTasks.Add(t);
        }
        var WhenAll = new Task<Task<int>>(async () =>
        {
            foreach (var t in ((IEnumerable<Task>)[..IoBoundTasks, ..CpuBoundTasks]))
            {
                t.Start();
            }
            var result = await Task.WhenAll([..IoBoundTasks, ..CpuBoundTasks]);
            return result.Select(task => task.Result).FirstOrDefault(1);
        });

        WhenAll.ConfigureAwait(false);
        Tasks = WhenAll;

        GC.Collect();
    }

    private int Run(TaskScheduler scheduler)
    {
        Tasks.ConfigureAwait(false);
        Tasks.Start(scheduler);
        Tasks.Wait();
        Tasks.Result.Wait();
        return Tasks.Result.Result;
    }

    [Benchmark(Baseline = true)]
    public int DefaultTaskScheduler()
    {
        return Run(TaskScheduler.Default);
    }

    [Benchmark]
    public int TargetJobScheduler()
    {
        var scheduler = new TargetJobScheduler(
            Environment.CurrentManagedThreadId,
            ThreadPriority.Normal,
            null,
            CancellationToken.None);
        return Run(scheduler);
    }

    [IterationCleanup]
    public void Cleanup()
    {
        Tasks = null!;
        GC.Collect();
    }
}
