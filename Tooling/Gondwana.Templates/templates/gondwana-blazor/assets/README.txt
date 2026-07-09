Place your game assets (PNG sprites, audio files, fonts, etc.) in this folder.

After adding a file, register it in your .csproj so it is copied to the output directory:

    <ItemGroup>
        <Content Include="assets\your-sprite.png">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </Content>
    </ItemGroup>

Then load it in MyGameHost.cs:

    Tilesheets:  Engine.Managers.Tilesheets.LoadFromImageFile("name", @"assets\your-sprite.png")
    Audio:       Engine.GetBrowserAudioManager().Load("name", "assets/theme.mp3")
    Fonts:       Engine.Managers.Fonts.LoadFromFile("name", @"assets\your-font.ttf")

NOTE: Audio files must be accessible as URLs relative to index.html
(use forward slashes: "assets/theme.mp3" not "assets\theme.mp3").

See https://github.com/Isthimius/Gondwana/wiki for full documentation.