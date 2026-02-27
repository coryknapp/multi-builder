using System;
using System.Threading;
using System.Threading.Tasks;

public class FullScreenViewService
{
    private IFullScreenView? currentView;
    private bool isRunning;
    private bool pauseUpdates;
    private bool pauseInput;
    private readonly object viewLock = new();

    public async Task ShowViewAsync(IFullScreenView view, CancellationToken cancellationToken = default)
    {
        lock (this.viewLock)
        {
            this.currentView = view;
            this.isRunning = true;
            this.pauseUpdates = false;
            this.pauseInput = false;
        }

        view.OnActivated();

        try
        {
            var inputTask = Task.Run(() => this.InputLoop(cancellationToken), cancellationToken);
            var renderTask = Task.Run(() => this.RenderLoop(cancellationToken), cancellationToken);

            await Task.WhenAny(inputTask, renderTask);
        }
        finally
        {
            this.isRunning = false;
            view.OnDeactivated();
        }
    }

    /// <summary>
    /// Pauses both rendering and input handling.
    /// </summary>
    public void Pause()
    {
        this.pauseUpdates = true;
        this.pauseInput = true;
    }

    /// <summary>
    /// Resumes both rendering and input handling.
    /// </summary>
    public void Resume()
    {
        this.pauseUpdates = false;
        this.pauseInput = false;
    }

    public void PauseUpdates() => this.pauseUpdates = true;

    public void ResumeUpdates() => this.pauseUpdates = false;

    public void Stop() => this.isRunning = false;

    private async Task RenderLoop(CancellationToken cancellationToken)
    {
        while (this.isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!this.pauseUpdates && this.currentView != null)
                {
                    this.currentView.Render();
                }

                var interval = this.currentView?.RefreshInterval ?? TimeSpan.FromMilliseconds(200);
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
        while (this.isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // When input is paused, don't consume any key input
                if (this.pauseInput)
                {
                    Thread.Sleep(50);
                    continue;
                }

                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(50);
                    continue;
                }

                var keyInfo = Console.ReadKey(intercept: true);

                if (this.currentView != null && !this.pauseInput)
                {
                    bool continueRunning = currentView.HandleKey(keyInfo);
                    if (!continueRunning)
                    {
                        this.isRunning = false;
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
}