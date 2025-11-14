# PlexampRPC
[![Latest Release](https://img.shields.io/github/v/release/Dyvinia/PlexampRPC?style=for-the-badge&labelColor=191919&color=e5a00d&label=Release)](https://github.com/Dyvinia/PlexampRPC/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Dyvinia/PlexampRPC/total?style=for-the-badge&labelColor=191919&color=e5a00d&label=Downloads)](https://github.com/Dyvinia/PlexampRPC/releases)

PlexampRPC displays currently playing Plex music as Discord Rich Presence, while using the proper Album Art.<br/> *Despite using the name Plexamp, it works regardless of the client used.*

<p float="left">
<img src="https://i.imgur.com/wbEsU6T.png" />
<img src="https://i.imgur.com/2x8HjMt.png" height="330" />
</p>

## Features
- Proper Album Art for your Music
- Uses Track Artist instead of Album Artist if available
- Custom "Listening to..." text
- Template to customize the info shown in the presence
- Load presence locally or from the server
- Start PlexampRPC on Windows startup
- Minimize to tray
- Self contained single .exe

## Options
Click the gear icon on the top right to open the Settings window. Here you can change different options, change the template, and easily see the current version of PlexampRPC.

![image](https://i.imgur.com/qtn8y6P.png)

The config file, aswell as the thumbnail cache and the stored auth token, are stored in `%AppData%\PlexampRPC\`. Press `F12` in the app to automatically open this folder.

### Template
These are all the placeholders that can be used for the template (the 3 text boxes at the bottom of the Settings window).
- `{title}`: Track Title
- `{artist}`: Artist
- `{album}`: Album
- `{year}`: Album Year
- `{player}`: Name of the device playing the music
- `{listens}`: Number of times listened to the track
- `{codec}`: Audio Codec
- `{container}`: File Container
- `{bitrate}`: Bitrate (Kbps)
- `{channel}`: Channel Layout (ex. Stereo)
- `{bitdepth}`: Bit Depth
- `{samplerate}` Sample Rate (kHz)

## FAQ
> **Q:** How do I change the "Listening to Plexamp" text?
> 
> **A:** In the settings window you can choose between "Listening to Plexamp", "Listening to Music", or a custom "Listening to ". To use a custom one, you must make a new application [here](https://discord.com/developers/applications) and set the Name you desire. Then open the config.json file in `%AppData%\PlexampRPC\` and paste the Application ID in. You can also have it say the track title or author in the member list with the "Status Display" setting.

> **Q:** My Plexamp can't get the now playing status, is it because I use someone else's Plex server to stream music?
> 
> **A:** It is. As a workaround, go to the settings window and enable `Use Local Player`. (Album Art will not show up with this)

> **Q:** How do I see the debug log?
> 
> **A:** Press the Icon to the left of the gear at the top left or press `F5`. When opened, you can press `Ctrl+C` to copy the log into the clipboard, or `Ctrl+S` to save it as a .txt file.

> **Q:** I can't run this
> 
> **A:** This requires [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and is Windows Only.

## Donate
[![Donate-Kofi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/J3J63UBHG)
