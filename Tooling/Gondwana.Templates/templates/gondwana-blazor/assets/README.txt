Place your game assets (PNG sprites, audio files, fonts, etc.) in this folder.

After adding a file, register it in your .csproj so it is copied to the output directory:

    <ItemGroup>
        <Content Include="assets\your-sprite.png">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </Content>
    </ItemGroup>

Then load it in MyGameHost.cs:

    Tilesheets:  Engine.Managers.Tilesheets.LoadFromImageFile("name", @"assets\your-sprite.png")
    Audio:       Engine.Managers.AudioResources.LoadFromUri("name", "assets/theme.mp3")
    Fonts:       Engine.Managers.Fonts.LoadFromFile("name", @"assets\your-font.ttf")

NOTE: Audio files must be accessible as URLs relative to index.html
(use forward slashes: "assets/theme.mp3" not "assets\theme.mp3").
Place browser audio in wwwroot/assets so it is served as a static web asset;
copying a file to the build output alone does not make it a browser URL.
Program.cs imports the JavaScript module and configures UseBrowserAudio().
Load during initialization, then call Play from a user gesture handler.
Browser autoplay policy and browser-supported codecs still apply.

See https://github.com/Isthimius/Gondwana/wiki for full documentation.
