namespace Gondwana.Tooling.Studio.Core.Services;

/// <summary>
/// Platform-neutral abstraction for file and user-interaction dialogs used by
/// framework-agnostic ViewModels. Each UI platform (Avalonia, WinForms, …)
/// provides a concrete implementation.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows an open-file picker and returns the selected path, or <see langword="null"/> if cancelled.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="patterns">Glob patterns to filter, e.g. <c>["*.png"]</c>.</param>
    Task<string?> OpenFileAsync(string title, string[] patterns);

    /// <summary>Shows an open-file picker that allows multiple selections.</summary>
    /// <param name="title">Dialog title.</param>
    Task<IReadOnlyList<string>> OpenFilesAsync(string title);

    /// <summary>Shows a save-file picker and returns the chosen path, or <see langword="null"/> if cancelled.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="suggestedName">Pre-filled file name.</param>
    /// <param name="defaultExt">Default extension without leading dot.</param>
    /// <param name="patterns">Glob patterns for the file-type filter.</param>
    Task<string?> SaveFileAsync(string title, string suggestedName, string defaultExt, string[] patterns);

    /// <summary>Shows a Yes/No confirmation dialog and returns <see langword="true"/> if Yes was chosen.</summary>
    Task<bool> ConfirmAsync(string message, string title);

    /// <summary>Shows an informational or error message.</summary>
    Task AlertAsync(string message, string title);

    /// <summary>Shows a text-input prompt and returns the entered text, or <see langword="null"/> if cancelled.</summary>
    /// <param name="message">Prompt text.</param>
    /// <param name="title">Dialog title.</param>
    /// <param name="defaultValue">Initial value pre-filled in the input field.</param>
    Task<string?> PromptAsync(string message, string title, string? defaultValue = null);

    /// <summary>Shows a picker for an <see cref="Gondwana.Assets.AssetTypes"/> value and returns
    /// the selected type name, or <see langword="null"/> if cancelled.</summary>
    Task<string?> PickAssetTypeAsync();
}
