Place browser assets in this wwwroot/assets folder: PNG images, GAF packages,
fonts, or audio.
The Blazor SDK includes wwwroot files automatically; no Content Include or
CopyToOutputDirectory entry is needed.

GameRenderSurface.razor contains DownloadAssetsAsync:
- Uncomment "assets/spot_defaults.png" for the loose PNG example.
- Uncomment "assets/game.gaf" for the packed GAF example.
Use app-relative URLs, forward slashes, and exact filename case.
Do not include wwwroot in the URL or use a leading slash.

MyGameHost.LoadTilesheets contains the matching commented examples.
Choose one for the "bubble" tilesheet, or use distinct names for multiple sheets.
The GAF example expects an Image entry named spot_defaults.png.
Change filenames and frame dimensions for your own assets.

Downloads finish before host.Initialize(). Bytes are passed into the host;
LoadAssets/LoadTilesheets remain synchronous. Do not make them async void.
Image streams and the sample GAF are disposed after synchronous image decoding.
If retaining an AssetsFile for later use, keep it alive and dispose it with the host.

Startup already displays loading/errors and cancels downloads on navigation.
No assets are downloaded until you enable entries in the download list.

Keyboard: uncomment BlazorKey registrations in OnKeyboardAdapterInitialized.
The handler filters KeyAction.Pressed; UnhookEvents removes the subscription.
Click the game canvas if keyboard focus has moved elsewhere.

Audio may use Engine.Managers.AudioResources.LoadFromUri with "assets/theme.mp3".
Program.cs configures the browser backend. Call Play from a user gesture handler;
browser autoplay policy and codec support still apply.
For fonts/data, download bytes and use stream decoding instead of filesystem APIs.

See https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Blazor/README.md
