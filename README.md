# Pac Dancegame Demo

A lightweight C# WPF dance-dance style demo that plays MP3s, shows background images, and flashes beat arrows on a configurable BPM timer.

## Features
- Auto-discovers MP3s dropped into `Assets/Songs`.
- Auto-matches background images from `Assets/Images` by filename and cycles through the image folder.
- Adjustable BPM, scrolling arrows, and hit-based scoring.

## Adding your own MP3s and images
1. Copy MP3 files into `Assets/Songs`.
2. Copy images (PNG/JPG) into `Assets/Images`.
3. Match filenames for auto pairing (example: `song.mp3` + `song.png`).

## Running the app
```bash
dotnet build
```
Then launch the generated executable from `bin/Debug/net8.0-windows` (or your preferred configuration).
