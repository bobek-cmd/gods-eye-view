namespace DualSenseBridge.Services;

/// <summary>Ensures one running instance; a second launch asks the first one to show its window.</summary>
public sealed class SingleInstance : IDisposable
{
    public const string MutexName = "DualSenseBridge.SingleInstance";
    private const string ShowEventName = "DualSenseBridge.ShowWindow";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _showEvent;
    private readonly RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent, Action onShowRequested)
    {
        _mutex = mutex;
        _showEvent = showEvent;
        _registration = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => onShowRequested(), null, -1, false);
    }

    /// <summary>Returns null when another instance already runs (after signalling it).</summary>
    public static SingleInstance? TryAcquire(Action onShowRequested)
    {
        var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            try
            {
                using var existing = EventWaitHandle.OpenExisting(ShowEventName);
                existing.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // First instance is still starting up.
            }

            return null;
        }

        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        return new SingleInstance(mutex, showEvent, onShowRequested);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _showEvent?.Dispose();
        try { _mutex.ReleaseMutex(); } catch (ApplicationException) { /* not owned */ }
        _mutex.Dispose();
    }
}
