using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using IndustrialTestPlatform.Contracts;
using IndustrialTestPlatform.Core;
using IndustrialTestPlatform.Supervisor;

namespace IndustrialTestPlatform.Wpf;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<SlotViewModel> _slots = new();
    private readonly ObservableCollection<string> _logs = new();
    private PlatformSupervisor? _supervisor;
    private CancellationTokenSource? _cts;
    private string _runStatus = "Idle";
    private bool _canStart = true;
    private bool _canStop;

    public MainViewModel()
    {
        StartCommand = new RelayCommand(async () => await StartAsync(), () => CanStart);
        StopCommand = new RelayCommand(Stop, () => CanStop);
        InitializeSlots();
    }

    public ObservableCollection<SlotViewModel> Slots => _slots;
    public ObservableCollection<string> Logs => _logs;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    public string RunStatus
    {
        get => _runStatus;
        private set
        {
            if (_runStatus != value)
            {
                _runStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CanStart
    {
        get => _canStart;
        private set
        {
            if (_canStart != value)
            {
                _canStart = value;
                OnPropertyChanged();
                ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanStop
    {
        get => _canStop;
        private set
        {
            if (_canStop != value)
            {
                _canStop = value;
                OnPropertyChanged();
                ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void InitializeSlots()
    {
        try
        {
            var configPath = ResolveConfigPath();
            var bundle = ConfigLoader.Load(configPath);
            foreach (var slot in bundle.Config.Slots)
            {
                _slots.Add(new SlotViewModel(slot.SlotId, slot.Name));
            }

            _supervisor = new PlatformSupervisor(bundle);
            _supervisor.SlotStatusChanged += (_, args) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var slotVm = _slots.FirstOrDefault(slot => slot.SlotId == args.SlotId);
                    if (slotVm is not null)
                    {
                        slotVm.Status = args.Status;
                        slotVm.LastMessage = args.Message;
                    }
                });
            };
            _supervisor.LogReceived += (_, args) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _logs.Insert(0, $"[{args.Entry.Timestamp:HH:mm:ss}] Slot {args.Entry.SlotId} {args.Entry.Event}: {args.Entry.Message}");
                });
            };
        }
        catch (Exception ex)
        {
            RunStatus = $"Config error: {ex.Message}";
        }
    }

    private async Task StartAsync()
    {
        if (_supervisor is null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        RunStatus = "Running";
        CanStart = false;
        CanStop = true;
        foreach (var slot in _slots)
        {
            slot.Status = SlotStatus.Running;
            slot.LastMessage = "Queued";
        }

        try
        {
            var runId = await _supervisor.StartAsync(_cts.Token).ConfigureAwait(false);
            Application.Current.Dispatcher.Invoke(() =>
            {
                RunStatus = $"Completed ({runId})";
            });
        }
        catch (OperationCanceledException)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RunStatus = "Stopped";
            });
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CanStart = true;
                CanStop = false;
            });
        }
    }

    private void Stop()
    {
        _cts?.Cancel();
        _supervisor?.Stop();
    }

    private static string ResolveConfigPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config", "sample-config.json");
        return Path.GetFullPath(path);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class SlotViewModel : INotifyPropertyChanged
{
    private SlotStatus _status = SlotStatus.Idle;
    private string _lastMessage = "Idle";

    public SlotViewModel(int slotId, string name)
    {
        SlotId = slotId;
        Name = name;
    }

    public int SlotId { get; }
    public string Name { get; }

    public SlotStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public string LastMessage
    {
        get => _lastMessage;
        set
        {
            if (_lastMessage != value)
            {
                _lastMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Func<Task>? _executeAsync;
    private readonly Action? _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _executeAsync = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        if (_executeAsync is not null)
        {
            await _executeAsync().ConfigureAwait(false);
        }
        else
        {
            _execute?.Invoke();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
