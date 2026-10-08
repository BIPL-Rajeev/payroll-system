namespace PayrollWeb.Services;

/// <summary>Toast message shown after an action (success or error).</summary>
public sealed record ToastMessage(string Message, string Type);

/// <summary>
/// Simple scoped toast service: pages call ShowSuccess/ShowError after actions,
/// MainLayout renders the current toast.
/// </summary>
public sealed class ToastService
{
    private readonly object _lock = new();
    private int _version;

    public ToastMessage? Current { get; private set; }

    public event Action? OnChange;

    public void ShowSuccess(string message) => Show(message, "success");

    public void ShowError(string message) => Show(message, "danger");

    private void Show(string message, string type)
    {
        int version;
        lock (_lock)
        {
            version = ++_version;
            Current = new ToastMessage(message, type);
        }

        OnChange?.Invoke();

        // Auto-dismiss after 5 seconds (unless replaced by a newer toast).
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            bool stillCurrent;
            lock (_lock)
            {
                stillCurrent = version == _version;
                if (stillCurrent)
                {
                    Current = null;
                }
            }
            if (stillCurrent)
            {
                OnChange?.Invoke();
            }
        });
    }

    public void Clear()
    {
        lock (_lock)
        {
            _version++;
            Current = null;
        }
        OnChange?.Invoke();
    }
}
