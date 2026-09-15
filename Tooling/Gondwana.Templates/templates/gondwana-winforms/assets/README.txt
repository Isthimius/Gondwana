Place your game assets (PNG sprites, audio files, fonts, etc.) in this folder.

After adding a file, register it in your .csproj so it is copied to the output directory:

    <ItemGroup>
        <Content Include="assets\your-sprite.png">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </Content>
    </ItemGroup>

Then load it in MyGameHost.cs:

    Tilesheets:  Engine.Managers.Tilesheets.LoadFromImageFile("name", @"assets\your-sprite.png")
    Audio:       Engine.Managers.AudioResources.LoadFromFile("name", @"assets\your-audio.mp3")
    Fonts:       Engine.Managers.Fonts.LoadFromFile("name", @"assets\your-font.ttf")

The WinForms game host configures Gondwana.Audio.NAudio before LoadAssets.
That Windows audio backend is included transitively through Gondwana.WinForms;
no additional initialization or package reference is needed for this template.

See https://github.com/Isthimius/Gondwana/wiki for full documentation.
