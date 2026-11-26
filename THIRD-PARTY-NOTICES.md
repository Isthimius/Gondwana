# Third-Party Notices

Gondwana's original source code is licensed under the [MIT License](LICENSE).
Third-party libraries, artwork, fonts, audio and other assets retain their
respective copyrights and license terms; the repository's MIT license does not
relicense those materials.

This document consolidates credits and provenance recorded throughout the
repository. It supplements, rather than replaces, the original license files,
copyright notices and distribution-specific obligations. A source credit is not
proof of permission to redistribute. Items requiring further verification are
identified explicitly.

## Engine and runtime libraries

### Skia

- Copyright: Google Inc.
- License: BSD 3-Clause.
- Project: [Skia](https://skia.org/).
- Existing notice: [skia.txt](Solution%20Items/licenses/skia.txt).
- Upstream license: [Skia LICENSE](https://skia.googlesource.com/skia/+/main/LICENSE).

### SkiaSharp

- Copyright (c) 2015–2016 Xamarin, Inc.; copyright (c) 2017–2018 Microsoft Corporation.
- License: MIT.
- Project: [SkiaSharp](https://github.com/mono/SkiaSharp).
- Existing license text: [skiasharp.txt](Solution%20Items/licenses/skiasharp.txt).
- Upstream license: [SkiaSharp LICENSE](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md).

### LibVLC

Gondwana.Video uses native LibVLC and its plugins, which retain their respective
licenses. See the [video README](Gondwana.Video/README.md) and the
[VideoLAN project](https://www.videolan.org/vlc/libvlc.html). Verify the notices
and license terms of the particular native binaries and plugins distributed.

## Studio and standalone editors

### Icons8

[Nature](https://icons8.com/icon/25449/nature) icon by [Icons8](https://icons8.com/).

The shared WinForms theming artwork also includes other `icons8-*.png` files
in [the theming assets directory](Tooling/Studio/Shared/WinForms/Theming/assets/).
Credit Icons8 for that artwork as well.

- Existing attribution: [attribution.txt](Tooling/Studio/Shared/WinForms/Theming/assets/attribution.txt).
- Licensing: [Icons8 license information](https://icons8.com/license).

The repository's existing attribution does not identify the acquired license
or subscription for these assets. Confirm the applicable terms, including
redistribution of source artwork and required attribution, before distribution.
Do not treat these icons as MIT-licensed.

## Demo assets

### Spot! — font

- Font: Architects Daughter (`ArchitectsDaughter-Regular.ttf`).
- Copyright (c) 2010, Kimberly Geswein (kimberlygeswein.com).
- License: SIL Open Font License 1.1.
- Original license and copyright notice: [OFL.txt](Demos/Spot.Shared/assets/OFL.txt).
- License information: [SIL Open Font License](https://openfontlicense.org/).

### Spot! — music and sound effects

Shared by the Windows, Avalonia and Blazor hosts. The original source links are
recorded in [sources.txt](Demos/Spot.Shared/assets/sources.txt); the asset
filenames identify the following contributors or uploader accounts.

| Use | Contributor/account indicated by filename | Recorded source |
| --- | --- | --- |
| Music | sounovamusic | [Puzzle & Casual Game Music](https://pixabay.com/music/happy-childrens-tunes-puzzle-amp-casual-game-music-460543/) |
| Velcro sound | freesound_community | [Velcro search](https://pixabay.com/sound-effects/search/velcro/?pagi=2) |
| Water drop | freesound_community | [Water drop search](https://pixabay.com/sound-effects/search/water%20drop/) |
| Game win | peekaboolabcreative | [Game win search](https://pixabay.com/sound-effects/search/game%20win/?pagi=2) |
| Game lose | freesound_community | [Game lose search](https://pixabay.com/sound-effects/search/game%20lose/) |
| Bump | freesound_community | [Bump search](https://pixabay.com/sound-effects/search/bump/) |
| Knock | rohhsadotcom | [Knock on Wood 02](https://pixabay.com/sound-effects/film-special-effects-knock-on-wood-02-421991/) |
| Spot selected/deselected | universfield | [Bubble Pop](https://pixabay.com/sound-effects/film-special-effects-bubble-pop-293342/) |

Source platform: [Pixabay](https://pixabay.com/). See its
[content license summary](https://pixabay.com/service/license-summary/).
The existing source record does not establish the exact download-time terms;
replace search-result links with verified individual asset pages and retain
the applicable licensing records. Account names are not independently
verified authorship claims.

### Slider — sound effects

The [Slider.WinForms assets directory](Demos/Slider.WinForms/assets/) contains
two WAV files with attribution-like identifiers in their filenames:

- `177120__rdholder__2dogsound-tadaa1-3s-2013jan31-cc-by-30-us.wav`:
  apparent contributor rdholder; filename indicates CC BY 3.0 US.
- `75143__willc2-45220__slide-cup-16b-44k-0-747s.wav`:
  apparent contributor willc2; license not documented.

Verify original sound pages, authors, actual licenses and modification
information. The filename alone is not a verified grant of permission.

### Space Duel — ship artwork

- Artwork: `ships.png`, extracted from `421718-PE6CZK-291.jpg`.
- Source image identifies [Freepik](https://www.freepik.com/) as designer/provider.
- Existing provenance and redistribution warning:
  [Assets README](Demos/Gondwana.SpaceDuel/Assets/README.md).

The extraction removed the backdrop and footer but did not change ownership.
Confirm the applicable Freepik license and attribution/redistribution rights
or replace the asset with properly licensed original artwork.

### Zelda Prototype — sprite sheets

The PNG sheets were obtained from
[The Spriters Resource: The Legend of Zelda: A Link to the Past](https://www.spriters-resource.com/snes/legendofzeldaalinktothepast/).
The game and artwork belong to their respective rights holders. The source
site's availability does not grant redistribution rights.

See the existing [art source and redistribution notice](Demos/Gondwana.ZeldaPrototype/README.md).
Permission for public redistribution is not established; local, non-commercial
experimentation is not itself a license to distribute these assets.

## Other repository assets

### Time Fantasy — Beach Tileset

- Artwork: Time Fantasy Beach Tileset, including `assets/beach_tileset.png`.
- Artist: finalbossblues.
- Projects: [Time Fantasy](https://timefantasy.net/),
  [artist website](https://finalbossblues.com/),
  [Patreon](https://www.patreon.com/finalbossblues).
- Original artist's notice: [assets/readme.txt](assets/readme.txt).

The original notice describes the tileset as a free download released as a
Patreon milestone reward. It does not specify redistribution or modification
terms. Verify those terms separately.

### TimGM6mb SoundFont

- Asset: [TimGM6mb.sf2](Gondwana.Audio.Midi/TimGM6mb.sf2), embedded by Gondwana.Audio.Midi.
- Upstream project/author identified by the [Debian package record](https://packages.debian.org/source/stable/timgm6mb-soundfont):
  Tim Brechbill.
- A [published copy of the SoundFont copyright and licensing record](https://github.com/arbruijn/TimGM6mb/blob/7a5b35d47baf5bae91ab55520b6838ff5bd16d6e/COPYING.txt)
  identifies copyright 2004 Tim Brechbill and GNU General Public License version 2 (GPL-2.0-only).

The repository currently lacks a SoundFont-specific license notice. Verify
that the embedded binary matches the applicable upstream release, retain its
full copyright and license texts, and review the implications of embedding
and distributing it in the MIDI assembly. This entry does not resolve those
obligations.

### Other legacy assets

The root [assets directory](assets/) contains additional images, audio,
video, packed assets, and a font archive
(`Architects_Daughter,Comic_Neue.zip`). The existing documentation does not
establish the provenance and license of every file or archive member. Review
these individually before redistribution; do not infer their licenses from
the repository's MIT license.

## Maintaining these notices

When adding a third-party asset or component, record its title, creator or
copyright holder, original source URL, license and applicable attribution,
repository location, and any modifications. Preserve original license files
and notices alongside the assets where required.

This index is not a complete inventory of direct and transitive NuGet
dependencies, native runtime libraries, or assets inside binary archives.
For each published package or application, retain the applicable notices,
license texts, and any required corresponding source or source offer for the
components actually distributed.
