# WiRide - Offline Motorcycle Intercom App

**MVP Version 1.0**

## 🎯 Überblick

WiRide ist eine Offline-Intercom-App für Motorradfahrer, die es mehreren Fahrern ermöglicht, über Wi-Fi Direct miteinander zu kommunizieren - **ohne Internetverbindung**.

## 🏗️ Architektur

### Host-Modus
- Erstellt Wi-Fi Soft-AP (Access Point)
- Empfängt Audio von allen Clients
- Mischt Audio-Streams mit **Mix-Minus** (jeder hört nur die anderen, nicht sich selbst)
- Verteilt gemischtes Audio an alle Clients

### Client-Modus
- Verbindet sich mit Host-Wi-Fi
- Sendet Mikrofon-Audio an Host
- Empfängt und spielt gemischtes Audio ab

## 📂 Projektstruktur

```
WiRide/
├── Models/
│   ├── AudioPacket.cs          # Audio-Daten-Paket
│   ├── NetworkMessage.cs       # Netzwerk-Nachricht
│   └── ClientInfo.cs           # Client-Informationen
├── Services/
│   ├── IAudioService.cs        # Audio-Interface
│   ├── INetworkService.cs      # Netzwerk-Interface
│   ├── IWiFiService.cs         # Wi-Fi-Interface
│   └── AudioMixerService.cs    # Audio-Mixing-Logik
├── Pages/
│   ├── StartPage.xaml          # Start (Host/Client Auswahl)
│   ├── HostPage.xaml           # Host-Ansicht
│   └── ClientPage.xaml         # Client-Ansicht
└── Platforms/Android/Services/
    ├── AndroidAudioService.cs  # Android Audio-Implementierung
    ├── AndroidWiFiService.cs   # Android Wi-Fi-Implementierung
    └── AndroidNetworkService.cs # Android Netzwerk-Implementierung
```

## 🔧 Technologien

- **.NET MAUI 10** - Cross-Platform UI Framework
- **Android AudioRecord/AudioTrack** - Audio Capture & Playback
- **UDP Sockets** - Low-Latency Audio-Streaming
- **Wi-Fi Direct / Soft-AP** - Offline-Verbindung

## ⚙️ Features

### ✅ Implementiert
- Host/Client Architektur
- Mute-Funktion
- Volume-Kontrolle

### 🚧 In Entwicklung
- Opus Audio-Codec (derzeit PCM)
- Verbesserte Hotspot-Verwaltung (Android 13+)
- Automatische Reconnection
- Latenz-Monitoring
- Einstellungen-Seite
- Mix-Minus Audio-Mixing
- Audio Capture & Playback
- UDP-basiertes Audio-Streaming
- Wi-Fi Soft-AP (Host)

## 📱 Anforderungen

### Hardware
- Android-Smartphone mit:
  - Wi-Fi Direct / Hotspot-Unterstützung
  - Mikrofon
  - Lautsprecher / Kopfhörer
- Mindestens Android 5.0 (API 21)

### Permissions
Die App benötigt folgende Berechtigungen:
- `RECORD_AUDIO` - Mikrofon-Zugriff
- `ACCESS_WIFI_STATE` - Wi-Fi-Status
- `CHANGE_WIFI_STATE` - Wi-Fi aktivieren/deaktivieren
- `ACCESS_FINE_LOCATION` - Wi-Fi-Scanning (Android 6+)
- `NEARBY_WIFI_DEVICES` - Hotspot (Android 13+)

## 🚀 Installation & Ausführung

### Voraussetzungen
- Visual Studio 2022 (17.13+) mit .NET 10 SDK
- Android SDK
- Android Emulator oder physisches Gerät

### Build & Run
```bash
# Restore Dependencies
dotnet restore

# Build
dotnet build

# Run auf Android
dotnet build -t:Run -f net10.0-android
```

## 📖 Verwendung

### Als Host:
1. App starten
2. "Start as Host" wählen
3. Wi-Fi Hotspot wird automatisch erstellt
4. Netzwerk-Name und Passwort notieren
5. Auf Clients warten

### Als Client:
1. App starten
2. "Join as Client" wählen
3. "Scan for Networks" drücken
4. WiRide-Netzwerk auswählen
5. Mit Host verbinden

### Während des Gesprächs:
- **Mute-Button** (Mikrofon-Icon): Stummschaltung ein/aus
- **Volume-Slider**: Lautstärke anpassen
- **Settings** (Zahnrad): Einstellungen (coming soon)

## 🎵 Audio-Spezifikationen

- **Sample Rate**: 16 kHz
- **Format**: PCM 16-bit
- **Channels**: Mono
- **Buffer Size**: 1024 bytes
- **Latency Target**: < 150 ms Round-Trip

## 🔒 Sicherheit & Datenschutz

- **Vollständig offline** - keine Daten verlassen das lokale Netzwerk
- **Kein Cloud-Service** - alle Daten bleiben auf den Geräten
- **Temporäre Daten** - Audio wird nicht gespeichert
- **Wi-Fi Passwort**: Standardmässig "WiRide123" (sollte geändert werden)

## 🐛 Bekannte Einschränkungen

1. **Android Hotspot**: Programmatisches Starten erfordert Systemberechtigungen (Android 8.0+)
   - Workaround: Hotspot manuell aktivieren
2. **Audio-Codec**: Derzeit PCM (unkomprimiert)
   - Geplant: Opus für bessere Kompression
3. **Reichweite**: Wi-Fi-abhängig (typisch 20-80m)
4. **Gleichzeitigkeit**: Performance hängt von Host-Gerät ab

## 🛠️ Entwicklung

### Service-Pattern
Alle plattformspezifischen Funktionen sind über Interfaces abstrahiert:
- `IAudioService` - Audio-Handling
- `IWiFiService` - Wi-Fi-Management
- `INetworkService` - Netzwerk-Kommunikation

### Audio-Mixing (Mix-Minus)
```csharp
// Für Client A: Mix von B + C (ohne A)
Audio_to_A = MixAudio(B.audio + C.audio) - A.audio

// Für Client B: Mix von A + C (ohne B)
Audio_to_B = MixAudio(A.audio + C.audio) - B.audio
```

---

**Made with ❤️ for Motorcycle Riders**
