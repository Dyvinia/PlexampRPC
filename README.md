# PlexampRPC
PlexampRPC displays currently playing Plex music as Discord Rich Presence, while using the proper Album Art.<br/> *Despite using the name Plexamp, it works regardless of the client used.*

<p float="left">
<img src="https://i.imgur.com/7jNWgUk.png" />
<img src="https://i.imgur.com/2syfqbT.png" height="313" />
<img src="https://i.imgur.com/decQuxm.png" height="313" />
</p>

## Features
- Proper Album Art for your Music
- Uses Track Artist instead of Album Artist if available 
- Start PlexampRPC on Windows startup
- Customize Template
- Minimize to tray
- Self contained single .exe

## Options & Template
Click the gear icon on the top right to open the Settings window. Here you can change different options, change the template, and easily see the current version of PlexampRPC.

![image](https://github.com/Dyvinia/PlexampRPC/assets/13797470/6ef36bd5-fa4d-4170-aa95-b8668e5c595d)

The config file, aswell as the thumbnail cache and the stored auth token, are stored in `%AppData%\PlexampRPC\`. Press `F12` in the app to automatically open this folder.

## FAQ
> **Q:** How do I change the Bold "Plexamp" text?
> 
> **A:** You must make a new application [here](https://discord.com/developers/applications) and set the Name you desire. Then open the config.json file in `%AppData%\PlexampRPC\` and paste the Application ID in.

> **Q:** How do I see a debug log?
> 
> **A:** Press `F5`, you can also press `Ctrl+C` to copy the log into the clipboard.

> **Q:** I can't run this
> 
> **A:** This requires [.NET 7 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) and is Windows Only.

## Donate
[![Donate-Kofi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/J3J63UBHG)
