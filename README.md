# Pac Dancegame Demo

A lightweight C# WPF dance-dance style demo that plays MP3s, shows background images, and flashes beat arrows on a configurable BPM timer.

## Features
- Auto-discovers MP3s dropped into `Assets/Songs`.
- Loads each song from its own folder in `Assets/Songs` and cycles background images from that folder.
- Adjustable BPM, scrolling arrows, and hit-based scoring.

## Adding your own MP3s and images
1. Create a folder per song in `Assets/Songs` (for example `Assets/Songs/MySong`).
2. Copy an MP3 into that folder.
3. Add `title.png` in the same folder for the song title image.
4. Add any additional PNG/JPG images in the folder for background cycling.
5. (Optional) Add an MP4 in the folder to play as a muted background video instead of cycling images.

## Running the app
```bash
dotnet build
```
Then launch the generated executable from `bin/Debug/net8.0-windows` (or your preferred configuration).
