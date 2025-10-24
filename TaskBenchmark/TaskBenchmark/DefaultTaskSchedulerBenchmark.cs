using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace TaskBenchmark;

[SimpleJob(RunStrategy.Monitoring, iterationCount:1)]
public class DefaultTaskSchedulerBenchmark
{
    public List<Task<Task>> IoBoundTasks = null!;
    public List<Task<Task>> CpuBoundTasks = null!;

    public Task<Task> WhenAll = null!;

    [IterationSetup]
    public void Setup()
    {
        IoBoundTasks = [];
        CpuBoundTasks = [];

        for (var i = 0; i != 128; i++)
        {
            var t = new Task<Task>(async () => await TaskGenerator.IoBoundTask());
            t.ConfigureAwait(false);
            IoBoundTasks.Add(t);
        }
        for (var i = 0; i != 512; i++)
        {
            var t = new Task<Task>(async () => await TaskGenerator.CpuBoundTask());
            t.ConfigureAwait(false);
            CpuBoundTasks.Add(t);
        }
        WhenAll = new Task<Task>(async () =>
        {
            foreach (var t in ((IEnumerable<Task>)[..IoBoundTasks, ..CpuBoundTasks]))
            {
                t.Start();
            }
            await Task.WhenAll([..IoBoundTasks, ..CpuBoundTasks]);
        });
        WhenAll.ConfigureAwait(false);
        GC.Collect();
    }

    [Benchmark]
    public void DefaultTaskScheduler()
    {
        WhenAll.Start(TaskScheduler.Default);
        WhenAll.ConfigureAwait(false);
        WhenAll.Wait();
        WhenAll.Result.Wait();
    }

    [Benchmark]
    public void TargetJobScheduler()
    {
        var scheduler = new TargetJobScheduler(
            Environment.CurrentManagedThreadId,
            ThreadPriority.Normal,
            null,
            CancellationToken.None);
        WhenAll.Start(scheduler);
        WhenAll.ConfigureAwait(false);
        WhenAll.Wait();
        WhenAll.Result.Wait();
    }

    [IterationCleanup]
    public void Cleanup()
    {
        IoBoundTasks = null!;
        CpuBoundTasks = null!;
        WhenAll = null!;
        GC.Collect();
    }
}
