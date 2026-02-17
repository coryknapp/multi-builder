
/// <summary>
/// Interface for full-screen views that can be displayed by FullScreenViewService.
/// </summary>
public interface IFullScreenView
{
    /// <summary>
    /// Called to render/update the display.
    /// </summary>
    void Render();

    /// <summary>
    /// Called when a key is pressed. Return false to exit the view.
    /// </summary>
    bool HandleKey(ConsoleKeyInfo keyInfo);

    /// <summary>
    /// Called when the view is first shown.
    /// </summary>
    void OnActivated();

    /// <summary>
    /// Called when the view is about to be hidden/closed.
    /// </summary>
    void OnDeactivated();

    /// <summary>
    /// Gets the desired refresh interval for this view.
    /// </summary>
    TimeSpan RefreshInterval { get; }
}