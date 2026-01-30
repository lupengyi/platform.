using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Platform.Core;
using Platform.Libraries;
using Platform.Tester;

namespace Platform.Supervisor.Wpf;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<string> _logs = new();
    private readonly ObservableCollection<SlotViewModel> _slots = new();
    private readonly CancellationTokenSource _cancellation = new();
    private StationConfig? _config;
    private Guid _runId;
    private string _status = "Idle";

    public MainViewModel()
    {
        var start = new AsyncRelayCommand(StartAsync, () => Status == "Idle");
        var stop = new RelayCommand(Stop, () => Status == "Running");
        StartCommand = start;
        StopCommand = stop;
        _ = LoadConfigAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    public ObservableCollection<SlotViewModel> Slots => _slots;
    public ObservableCollection<string> Logs => _logs;

    public string StationName => _config?.StationName ?? "";
    public int SlotCount => _config?.SlotCount ?? 0;
    public int Columns => _config?.Columns ?? 1;
    public IEnumerable<string> StepPlan => _config?.StepPlan ?? Array.Empty<string>();

    public string RunId => _runId == Guid.Empty ? "-" : _runId.ToString();

    public string Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }
    }

    private void RaiseCanExecuteChanged()
    {
        if (StartCommand is AsyncRelayCommand asyncCommand)
        {
            asyncCommand.RaiseCanExecuteChanged();
        }
        if (StopCommand is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task LoadConfigAsync()
    {
        var basePath = AppContext.BaseDirectory;
        var appConfig = await ConfigLoader.LoadAppConfigAsync(Path.Combine(basePath, "config", "appsettings.json"), _cancellation.Token);
        _config = await ConfigLoader.LoadStationConfigAsync(Path.Combine(basePath, "config", appConfig.StationConfigPath), _cancellation.Token);
        _slots.Clear();
        for (var i = 1; i <= _config.SlotCount; i++)
        {
            _slots.Add(new SlotViewModel(i));
        }
        OnPropertyChanged(nameof(StationName));
        OnPropertyChanged(nameof(SlotCount));
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(StepPlan));
    }

    private async Task StartAsync()
    {
        if (_config is null)
        {
            return;
        }

        Status = "Running";
        _runId = Guid.NewGuid();
        OnPropertyChanged(nameof(RunId));
        _logs.Clear();

        var basePath = AppContext.BaseDirectory;
        var appConfig = await ConfigLoader.LoadAppConfigAsync(Path.Combine(basePath, "config", "appsettings.json"), _cancellation.Token);
        var limits = await LimitCsvParser.LoadAsync(Path.Combine(basePath, "config", appConfig.LimitsCsvPath), _cancellation.Token);
        var logger = new UiLogger(_logs);
        var reportRoot = Path.Combine(basePath, "reports");
        var station = new StationController(_config, limits, logger, reportRoot);

        var summary = await station.RunAsync(_cancellation.Token);
        Status = "Idle";
        _logs.Add($"Run complete. Passed {summary.Passed} Failed {summary.Failed}.");
        UpdateSlots(summary);
    }

    private void UpdateSlots(RunSummary summary)
    {
        foreach (var slot in _slots)
        {
            slot.State = summary.RunId == Guid.Empty ? "Idle" : "Complete";
        }
    }

    private void Stop()
    {
        _cancellation.Cancel();
        Status = "Idle";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class SlotViewModel : INotifyPropertyChanged
{
    private string _state = "Idle";
    private string _lastMeasurement = "-";
    private string _lastOutcome = "-";

    public SlotViewModel(int slotId)
    {
        SlotId = slotId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SlotId { get; }
    public string Title => $"Slot {SlotId:00}";

    public string State
    {
        get => _state;
        set { _state = value; OnPropertyChanged(); }
    }

    public string LastMeasurement
    {
        get => _lastMeasurement;
        set { _lastMeasurement = value; OnPropertyChanged(); }
    }

    public string LastOutcome
    {
        get => _lastOutcome;
        set { _lastOutcome = value; OnPropertyChanged(); }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class UiLogger : ILogger
{
    private readonly ObservableCollection<string> _logs;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;

    public UiLogger(ObservableCollection<string> logs)
    {
        _logs = logs;
        _dispatcher = System.Windows.Application.Current.Dispatcher;
    }

    public void Log(Platform.Contracts.LogEntry entry)
    {
        _dispatcher.Invoke(() =>
        {
            _logs.Add($"{entry.TimestampUtc:HH:mm:ss} [S{entry.SlotId}] {entry.Level} {entry.Message}");
        });
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute();

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool> _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting && _canExecute();

    public async void Execute(object? parameter)
    {
        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
