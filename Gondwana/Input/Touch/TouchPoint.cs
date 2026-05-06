using System.Drawing;

namespace Gondwana.Input.Touch;

/// <summary>
/// Represents a single touch contact point, capturing its unique identifier, screen position,
/// and the current phase of its lifecycle.
/// </summary>
/// <param name="Id">
/// The unique identifier for this touch contact. The same finger contact retains the same
/// <see cref="Id"/> across <see cref="TouchPhase.Began"/>, <see cref="TouchPhase.Moved"/>,
/// and <see cref="TouchPhase.Ended"/> phases. On desktop, a mouse-emulated touch always uses
/// <c>Id = 0</c>.
/// </param>
/// <param name="Position">
/// The position of the touch contact in client (control-local) coordinates at the moment
/// the event was recorded.
/// </param>
/// <param name="Phase">
/// The current lifecycle phase of this touch contact, indicating whether the contact just
/// began, moved, remained stationary, ended, or was cancelled.
/// </param>
public readonly record struct TouchPoint(int Id, Point Position, TouchPhase Phase);
