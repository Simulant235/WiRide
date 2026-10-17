# WiRide - Offline Motorcycle Intercom App

Version 0.8.1 · .NET MAUI 10 · Android

## 🎯 Überblick

WiRide ist ein Intercom für Motorradgruppen ohne Internet und ohne Mobilfunk. Ein Handy ist **Host** (stellt das WLAN bereit und mischt das Audio), alle anderen sind **Clients**. Jeder hört alle anderen, aber nicht sich selbst (Mix-Minus).

## 🏗️ Architektur

```
Client-Mikrofon ──Opus/UDP──►  Host  ──Opus/UDP──► Client-Lautsprecher
                               │  Mix-Minus alle 20 ms:
                               │  Summe aller Stimmen minus eigene Stimme
Host-Mikrofon ────────────────►│──────────────────► Host-Lautsprecher
```

### Host-Modus
- Startet einen WLAN-Hotspot (LocalOnlyHotspot, SSID/Passwort werden angezeigt) oder nutzt einen manuellen Hotspot / ein bestehendes WLAN
- Lauscht auf UDP-Port 8888 (einstellbar), beantwortet Host-Suchen per Broadcast
- Mischt alle 20 ms einen Frame pro Teilnehmer (Jitter-Buffer 80 ms) und schickt jedem seinen eigenen Mix
- Entfernt Clients nach 5 s ohne Lebenszeichen

### Client-Modus
- Sucht den Host im aktuellen WLAN (Broadcast + Gateway-IP)
- Falls keiner gefunden: WiRide-WLAN aus Scan wählen oder Host-IP manuell eingeben
- Sendet Mikrofon-Frames, spielt den Mix ab (Puffer max. 100 ms)
- KeepAlive-Ping jede Sekunde mit Latenzanzeige, automatischer Reconnect bei Abbruch

## 📂 Projektstruktur

```
WiRide/
├── Models/
│   ├── NetworkMessage.cs        # Binäres UDP-Protokoll (Discover, Join, Audio, Ping/Pong ...)
│   ├── AudioPacket.cs           # Empfangenes Audio inkl. Codec
│   └── ClientInfo.cs            # Client-Infos + DiscoveredHost
├── Services/
│   ├── IntercomService.cs       # Verbindet Audio, Netzwerk, Mixer, Codec (Host/Client-Sitzung)
│   ├── UdpNetworkService.cs     # Plattformunabhängiger UDP-Service
│   ├── AudioMixerService.cs     # Mix-Minus für PCM 16-bit
│   ├── AudioCodecService.cs     # Opus-Kompression (Concentus)
│   ├── AppSettings.cs           # Persistente Einstellungen
│   ├── PermissionHelper.cs      # Laufzeit-Berechtigungen
│   ├── ServiceHelper.cs         # DI-Zugriff aus Pages
│   └── I*Service.cs             # Interfaces
├── Pages/
│   ├── StartPage                # Host/Client-Auswahl
│   ├── HostPage                 # Host-Ansicht mit Rider-Liste und Latenzen
│   ├── ClientPage               # Client-Ansicht mit Verbindungsstatus und Latenz
│   ├── NetworkSelectionPage     # WLAN-Auswahl
│   ├── SettingsPage             # Einstellungen
│   └── AudioTestPage            # Nur DEBUG: lokaler Audio-Test
└── Platforms/Android/Services/
    ├── AndroidAudioService.cs           # AudioRecord/AudioTrack (VoiceCommunication)
    ├── AndroidWiFiService.cs            # Hotspot, Scan, WLAN-Verbindung
    └── IntercomForegroundService.cs     # Hintergrundbetrieb
```

## 🔧 Technologien

- .NET MAUI 10, C#
- `System.Net.Sockets.UdpClient` mit eigenem Binärprotokoll (20 Byte Header)
- Android `AudioRecord` / `AudioTrack` (Echo-Unterdrückung über VoiceCommunication)
- [Concentus](https://github.com/lostromb/concentus) (Opus in reinem C#)
- Android `LocalOnlyHotspot`, `WifiNetworkSpecifier`, Foreground-Service

## ⚙️ Features

- ✅ Host/Client-Intercom über WLAN (UDP)
- ✅ Mix-Minus (niemand hört sich selbst)
- ✅ Opus-Kompression (~45 statt 640 Byte pro 20 ms), abschaltbar
- ✅ Hotspot per App, Fallback auf manuellen Hotspot oder bestehendes WLAN
- ✅ Automatische Host-Suche, manuelle IP als Fallback
- ✅ WLAN-Scan und Verbindung aus der App
- ✅ Latenzanzeige (Client und pro Rider beim Host)
- ✅ Auto-Reconnect und Client-Timeout
- ✅ Mute (echter Mikrofon-Mute)
- ✅ Einstellungen: Name, Lautstärke, Auto-Reconnect, Opus, Port, Sample-Rate
- ✅ Hintergrundbetrieb bei gesperrtem Bildschirm

## 📱 Anforderungen

### Hardware
- Android 8.0+ für den App-Hotspot (ältere Geräte: manueller Hotspot)
- Android 10+ empfohlen für WLAN-Verbindung aus der App
- Bluetooth-Headset im Helm (Audio läuft über das Standard-Audiogerät)

### Berechtigungen
- Mikrofon (`RECORD_AUDIO`)
- Standort (`ACCESS_FINE_LOCATION`, für WLAN-Scan und Hotspot; Standort muss eingeschaltet sein)
- Geräte in der Nähe (`NEARBY_WIFI_DEVICES`, Android 13+)
- Benachrichtigungen (`POST_NOTIFICATIONS`, Android 13+, für den Hintergrundbetrieb)
- WLAN/Netzwerk, Multicast, Foreground-Service, WakeLock

## 🚀 Installation & Ausführung

### Voraussetzungen
- Visual Studio 2026 mit .NET MAUI Workload
- .NET 10 SDK, Android SDK

### Build & Run
```bash
dotnet build WiRide/WiRide.csproj -f net10.0-android
```
In Visual Studio: Android-Gerät auswählen und starten.

> Hinweis: Die `nuget.config` im Repo ergänzt das globale Package-Source-Mapping um `Concentus`.

## 📖 Verwendung

### Als Host
1. **Host** wählen, Berechtigungen erlauben
2. **WiRide-Hotspot starten** (oder manuellen Hotspot / aktuelles WLAN)
3. WLAN-Name, Passwort und IP werden unten angezeigt
4. Verbundene Rider erscheinen mit ihrer Latenz

### Als Client
1. **Client** wählen, **Connect to Network** tippen
2. Ist das Handy schon im WLAN des Hosts, wird er automatisch gefunden
3. Sonst **WiRide-WLAN wählen** (Passwort vom Host eingeben) oder **Host-IP manuell eingeben**

### Während der Fahrt
- Mikrofon-Kachel tippen = Mute (🎤 ↔ 🔇)
- Bildschirm kann gesperrt werden, die Benachrichtigung zeigt die aktive Sitzung
- Bei Verbindungsabbruch verbindet der Client automatisch neu

## 🎵 Audio-Spezifikationen

| Parameter | Wert |
|---|---|
| Sample-Rate | 16 kHz (einstellbar: 8/16/24/48 kHz, auf allen Geräten gleich) |
| Format | PCM 16-bit Mono |
| Frame | 20 ms (320 Samples bei 16 kHz) |
| Codec | Opus VoIP, 24 kbit/s (oder unkomprimiert PCM) |
| Jitter-Buffer Host | max. 4 Frames (80 ms) |
| Abspiel-Puffer | max. 5 Frames (100 ms) |

## 🔒 Sicherheit & Datenschutz

- Kein Internet, keine Server, keine Aufzeichnung
- Audio bleibt im lokalen WLAN; der Hotspot ist WPA2-geschützt
- Die Audio-Pakete selbst sind nicht verschlüsselt (im WLAN mitlesbar, wer das Passwort kennt)

## 🐛 Bekannte Einschränkungen

- Nur Android; iOS/Windows nutzen Platzhalter für Audio und WLAN (iOS erlaubt keinen App-Hotspot, der Build braucht einen Mac)
- LocalOnlyHotspot: SSID und Passwort vergibt Android zufällig (nicht konfigurierbar)
- WLAN-Scan ist von Android gedrosselt (4 Scans / 2 Minuten)
- Port und Sample-Rate müssen auf allen Geräten gleich sein
- Reichweite hängt vom WLAN ab (typisch 30–100 m)

## 🛠️ Entwicklung

### Versionierung
`vX.Y.Z - Titel` pro Commit, Feature-Branches werden mit `--no-ff` in `master` gemergt.
- **X**: 1 sobald alle Grundfunktionen fertig nutzbar sind (sonst 0)
- **Y**: neues Feature
- **Z**: Patch

Siehe [ROADMAP.md](../ROADMAP.md) für Changelog und offene Punkte.

### Protokoll
Jedes UDP-Paket: `'W' 'R' Version Typ SenderId(16)` + Nutzdaten.
`Audio`: Sequenz (int32), Codec (byte), Audiodaten. `Ping/Pong`: Zeitstempel (int64), Latenz (int32).
