using System;
using System.Threading;
using System.Threading.Tasks;

public class FullScreenViewService
{
    private IFullScreenView? _currentView;
    private bool _isRunning;
    private bool _pauseUpdates;
    private readonly object _viewLock = new();

    public async Task ShowViewAsync(IFullScreenView view, CancellationToken cancellationToken = default)
    {
        lock (_viewLock)
        {
            _currentView = view;
            _isRunning = true;
            _pauseUpdates = false;
        }

        view.OnActivated();

        try
        {
            var inputTask = Task.Run(() => InputLoop(cancellationToken), cancellationToken);
            var renderTask = Task.Run(() => RenderLoop(cancellationToken), cancellationToken);

            await Task.WhenAny(inputTask, renderTask);
        }
        finally
        {
            _isRunning = false;
            view.OnDeactivated();
        }
    }

    public void PauseUpdates() => _pauseUpdates = true;

    public void ResumeUpdates() => _pauseUpdates = false;

    public void Stop() => _isRunning = false;

    private async Task RenderLoop(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_pauseUpdates && _currentView != null)
                {
                    _currentView.Render();
                }

                var interval = _currentView?.RefreshInterval ?? TimeSpan.FromMilliseconds(200);
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Prevent crashes from rendering issues
            }
        }
    }

    private void InputLoop(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(50);
                    continue;
                }

                var keyInfo = Console.ReadKey(intercept: true);

                if (_currentView != null)
                {
                    bool continueRunning = _currentView.HandleKey(keyInfo);
                    if (!continueRunning)
                    {
                        _isRunning = false;
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Temporarily shows a different view (e.g., log viewer), then returns to the previous view.
    /// </summary>
    public async Task ShowModalViewAsync(IFullScreenView modalView, CancellationToken cancellationToken = default)
    {
        var previousView = _currentView;
        var wasPaused = _pauseUpdates;

        PauseUpdates();
        await Task.Delay(100, cancellationToken); // Allow current render to complete

        try
        {
            await ShowViewAsync(modalView, cancellationToken);
        }
        finally
        {
            lock (_viewLock)
            {
                _currentView = previousView;
                _pauseUpdates = wasPaused;
                _isRunning = previousView != null;
            }
        }
    }
}