namespace Gondwana.Widgets.Controls;

/// <summary>
/// Defines a platform-specific service for opening external URIs.
/// </summary>
public interface IExternalUriLauncher
{
    /// <summary>
    /// Opens the specified URI using the platform's external URI handler.
    /// </summary>
    /// <param name="uri">The absolute URI to open.</param>
    /// <param name="cancellationToken">
    /// A token used to request cancellation of the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous open operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="uri"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current platform cannot open the specified URI.
    /// </exception>
    ValueTask OpenAsync(Uri uri, CancellationToken cancellationToken = default);
}
