namespace TaskBenchmark;

using System.Threading.Channels;

/// <summary>
/// from https://github.com/ChadBurggraf/parallel-extensions-extras/tree/master/TaskSchedulers
/// </summary>
public sealed class TargetJobScheduler : TaskScheduler, IDisposable
{
    private readonly Channel<Task> _channel;
    private readonly ChannelWriter<Task> _writer;
    private readonly ChannelReader<Task> _reader;
    private readonly Thread[] _workers;
    private readonly CancellationTokenSource _source = new();
    private readonly CancellationToken _token;
    private readonly TaskFactory _choreTaskFactory;
    private const string ThreadNamePrefix = nameof(TargetJobScheduler);

    public TargetJobScheduler(int maxConcurrency, ThreadPriority priority,TaskFactory? choreTaskFactory,CancellationToken token)
    {
        MaximumConcurrencyLevel = maxConcurrency;

        if (choreTaskFactory == null)
        {
            _choreTaskFactory = new TaskFactory(
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskContinuationOptions.None,
                this);
        }
        else
        {
            _choreTaskFactory = choreTaskFactory;
        }

        var options = new BoundedChannelOptions(maxConcurrency * 128)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _channel = Channel.CreateBounded<Task>(options);
        _writer = _channel.Writer;
        _reader = _channel.Reader;
        _token = CancellationTokenSource.CreateLinkedTokenSource(token, _source.Token).Token;

        _workers = new Thread[maxConcurrency];
        for (int i = 0; i < maxConcurrency; i++)
        {
            _workers[i] = new Thread(WorkerLoop)
            {
                Name = $"{ThreadNamePrefix}-unknown",
                Priority = priority,
                IsBackground = true,
            };
            _workers[i].Start();
        }
    }

    protected override void QueueTask(Task task)
    {
        if (_token.IsCancellationRequested)
            return;

        if (!_writer.TryWrite(task))
        {
            // 如果队列满了，异步写入
            _choreTaskFactory.StartNew(async () => await _writer.WriteAsync(task,_token),_token);
        }
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        // 如果任务之前已经入队，不允许内联执行
        if (taskWasPreviouslyQueued)
            return false;

        // 如果当前线程是我们的工作线程之一，允许内联执行
        if (Thread.CurrentThread.Name?.StartsWith(ThreadNamePrefix) == true)
            return TryExecuteTask(task);

        return false;
    }

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        throw new NotSupportedException("Not support to get tasks for TargetJobScheduler");
    }

    public override int MaximumConcurrencyLevel { get; }

    private void WorkerLoop()
    {
        Thread.CurrentThread.Name = $"{ThreadNamePrefix}-{Environment.CurrentManagedThreadId}";

        while (!_token.IsCancellationRequested)
        {
            try
            {
                var read = _reader.ReadAsync(_token).AsTask();
                read.ConfigureAwait(false);
                read.Wait(_token);

                if (read.IsCompletedSuccessfully)
                {
                    TryExecuteTask(read.Result);
                }
                else if (read.Exception != null)
                {
                    throw read.Exception;
                }
            }
            catch (OperationCanceledException) when (_token.IsCancellationRequested)
            {
                // do nothing
            }
        }
    }

    public void Dispose()
    {
        _source.Cancel();
        _writer.Complete();

        try
        {
            _ = _workers.Select(thread => thread.Join(5000)).ToArray();
        }
        catch (AggregateException)
        {
            // 忽略取消异常
        }

        _source.Dispose();
    }
}
