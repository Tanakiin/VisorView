# VisorView

**VisorView** is a lightweight, always-on-top overlay for Windows that lets you pin **webpages or images** above everything else — think documentation, maps, references, or dashboards that stay visible while you work or game.

The goal is simple: *zero friction, zero clutter, maximum utility.*

---

## Features

- 🪟 Always-on-top overlay window  
- 🌐 Embedded browser tabs (WebView-based)  
- 🖼️ Image tabs with aspect-ratio–aware resizing  
- 🔍 Per-tab zoom controls for browser content  
- 🧲 Snap-to-min sizing when switching content types  
- 🧠 State-aware UI (browser vs image behavior)  
- 🎯 Minimal, unobtrusive controls by design  

Built to feel invisible until you need it.

---

## Tech Stack

- .NET 8  
- WPF (Windows Presentation Foundation)  
- WebView2 (Chromium-based)  
- XAML + C#  
- WinForms interop (native file dialogs)  
- Single-file, self-contained publishing  

No Electron. No heavyweight frameworks. Native Windows all the way.

---

Clean separation between UI, state, and behavior — without overengineering.

---

## Building & Running

```
dotnet build
dotnet run
```

### Single-file publish (Windows x64)

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

This produces a single executable that runs without requiring .NET to be installed.

---

## Download

➡️ **[Download VisorView (Windows)](LINK_GOES_HERE)**  
*(release builds will be posted here)*

---

## Why VisorView?

The original frustration came from gaming, specifically **Elden Ring**.

Constantly alt-tabbing to look up **smithing stone locations**, **NPC quest steps**, or **boss weaknesses** breaks immersion fast. You either lose focus, miss a timing window, or just get tired of bouncing between the game and a browser.

I wanted something that:

- stays visible without stealing focus  
- lets me keep a wiki, map, or notes on-screen  
- doesn’t feel like a second app competing for attention  
- works just as well for development docs as it does for games  

VisorView grew out of that exact pain point.

---

## Author

Built by **Tanay Reddy**  
https://tanayreddy.org
