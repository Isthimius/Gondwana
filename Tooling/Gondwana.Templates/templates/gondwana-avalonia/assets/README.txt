Place your game assets (PNG sprites, audio files, fonts, etc.) in this folder.

After adding a file, register it in your .csproj so it is copied to the output directory:

    <ItemGroup>
        <Content Include="assets\your-sprite.png">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </Content>
    </ItemGroup>

Then load it in MyGameHost.cs:

    Tilesheets:  Engine.Managers.Tilesheets.LoadFromImageFile("name", @"assets\your-sprite.png")
    Fonts:       Engine.Managers.Fonts.LoadFromFile("name", @"assets\your-font.ttf")

Audio requires an explicitly selected compatible backend. Configure it in
MyGameHost.OnInitializing before loading sounds in LoadAssets, for example:

    Engine.Managers.AudioResources.LoadFromFile("name", "assets/your-audio.mp3")

The backend must support file/byte sources. This cross-platform net8.0 template
has no default desktop audio backend. Gondwana.Audio.NAudio and Gondwana.Audio.Midi
require Windows targeting; gondwana add audio/midi will explain this limitation
without changing this project's target framework or adding those packages.

See https://github.com/Isthimius/Gondwana/wiki for full documentation.
