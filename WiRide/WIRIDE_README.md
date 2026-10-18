# WiRide - Offline Motorcycle Intercom App

Version 1.7.0 · .NET MAUI 10 · Android

## 🎯 Überblick

WiRide ist ein Intercom für Motorradgruppen ohne Internet und ohne Mobilfunk. Ein Handy ist **Host** (stellt das WLAN bereit und mischt das Audio), alle anderen sind **Clients**. Jeder hört alle anderen, aber nicht sich selbst (Mix-Minus). Alle Einstellungen gelten nur für das eigene Handy.

## 🏗️ Architektur

```
Client-Mikrofon ─(Rauschfilter/VAD)─Opus/UDP─►  Host  ──Opus/UDP──► Client-Lautsprecher
                                                 │  alle 20 ms pro Zuhörer:
                                                 │  Summe aller Stimmen − eigene Stimme
                                                 │  (+ Normalisierung nach Wunsch des Zuhörers,
                                                 │   + geteilte Musik, beim Sprechen abgesenkt)
Host-Mikrofon ──────────────────────────────────►│──────────────────► Host-Lautsprecher
```

- **Host:** Wi-Fi-Direct-Gruppe mit eigenem Passwort (oder eigener Handy-Hotspot), UDP-Port 8888, Mix-Minus mit Jitter-Buffer, verteilt Ereignisse (Beitritt, Schnell-Ansagen) an alle.
- **Client:** Beitritt per QR-Code, Suche oder WLAN-Auswahl; KeepAlive mit Latenz, Akku und Signal; Auto-Reconnect.

## 📂 Projektstruktur

```
WiRide/
├── Models/                       # Binärprotokoll (NetworkMessage), AudioPacket, ClientInfo
├── Services/
│   ├── IntercomService.cs        # Sitzung: Audio, Netzwerk, Mixer, Codec, Ansagen, Ducking, Musik
│   ├── UdpNetworkService.cs      # UDP: Discover, Join, Audio, Music, Event, Ping/Pong
│   ├── AudioMixerService.cs      # Mix-Minus, Normalisierung, Musik-Absenkung
│   ├── AudioProcessor.cs         # Hochpass, Rauschsperre, Sprachaktivierung
│   ├── AudioCodecService.cs      # Opus (Concentus)
│   ├── AnnouncementService.cs    # Sprachansagen (Android-TTS oder Piper)
│   ├── PiperVoiceManager.cs      # KI-Stimmen: Katalog, Download, Entpacken
│   ├── SpeedVolumeService.cs     # Lautstärke nach Tempo (GPS)
│   ├── SessionStatus.cs          # Status für Widget und Mini-Fenster
│   ├── Loc.cs / ThemeManager.cs  # Sprachen, Design, Schriftgröße
│   └── AppSettings.cs            # Alle Einstellungen (Preferences)
├── Pages/                        # Start, Host, Client, QR, Einführung, Feedback, Mikro-Test
│   └── Settings/                 # Einstellungs-Kategorien (per Code aufgebaut, alphabetisch)
├── Resources/Strings/*.json      # 27 Sprachen
└── Platforms/Android/            # Audio, WLAN/Hotspot, Piper, Musik-Mitschnitt,
                                  # Foreground-Service, Widget, Mini-Fenster
```

## ⚙️ Features

**Intercom**
- Mix-Minus, Opus (~93 % weniger Daten), Latenz-Modus (niedrig / Kompatibilität)
- Rauschunterdrückung, Normalisierung und Sprachaktivierung (experimentell), jeweils mit Stärke
- Lautstärke nach Tempo (GPS)
- Hintergrundbetrieb bei gesperrtem Bildschirm, „Beenden“ in der Benachrichtigung

**Verbindung**
- Hotspot per App mit eigenem, gespeichertem Passwort (Zeichensatz wählbar) oder eigener Hotspot
- QR-Code zum Beitreten (Standard-WLAN-Format, auch mit der Handy-Kamera)
- Schnellstart: „Erneut hosten“ bzw. „Wieder verbinden“ (nur aktiv, wenn der Host online ist)
- Rider-Liste mit Akku, Signal und Latenz

**Ansagen & Musik**
- Sprachansagen in der eigenen Sprache (Rider beigetreten/verlassen, Verbindung)
- Stimme: Android-Sprachausgabe oder KI-Stimme Piper (23 Sprachen, je ca. 64 MB Download)
- Schnell-Ansagen: Pause, Tanken, Polizei, Gefahr, Langsamer, Abbiegen, Warten, Alles ok
- Ducking: Spotify/Navi werden leiser, sobald jemand spricht
- Musik teilen (experimentell, nur Apps, die Mitschnitt erlauben)

**Oberfläche**
- 27 Sprachen, helles/dunkles Design, 4 Schriftgrößen
- Einstellungen in Kategorien, alphabetisch sortiert, speichern sofort
- Home-Widget, schwebendes Mini-Fenster, Mikrofon-Test, Headset-Warnung, Einführung
- Feedback-Formular (Feedback, Fehler, Übersetzung) per E-Mail

## 📱 Anforderungen

- Android 8.0+ (App-Hotspot mit eigenem Passwort ab Android 10)
- Bluetooth-Headset im Helm empfohlen
- Berechtigungen: Mikrofon, Standort, Geräte in der Nähe, Benachrichtigungen, Kamera (QR), optional „Über anderen Apps einblenden“ (Mini-Fenster)

## 🚀 Build

```bash
dotnet build WiRide/WiRide.csproj -f net10.0-android -p:TargetFrameworks=net10.0-android
```

Release-APK:

```bash
dotnet publish WiRide/WiRide.csproj -f net10.0-android -c Release -p:TargetFrameworks=net10.0-android -p:AndroidPackageFormat=apk
```

Die `nuget.config` im Repo ordnet alle Pakete nuget.org zu (globales Package-Source-Mapping).

## 🌍 Neue Sprache / Übersetzung verbessern

`Resources/Strings/en.json` kopieren, als `<code>.json` speichern und übersetzen. Platzhalter wie `{0}` und `\n` unverändert lassen. Die Sprache in `Loc.Languages` eintragen.

## 🐛 Bekannte Einschränkungen

- Nur Android (iOS erlaubt keinen App-Hotspot und braucht einen bezahlten Apple-Account)
- WLAN-Passwort mind. 8 Zeichen (WPA2); unter Android 10 vergibt Android das Hotspot-Passwort
- Musik teilen funktioniert nicht mit Spotify (Mitschnitt gesperrt)
- KI-Stimme fehlt für Japanisch, Koreanisch, Thai und Filipino (dort Android-Stimme)
- Feedback-Adresse muss noch in `FeedbackPage.FeedbackEmail` eingetragen werden
- Übersetzungen außer Deutsch/Englisch sind maschinell erstellt

## 🛠️ Versionierung

`vX.Y.Z - Titel` pro Commit, Feature-Branches werden mit `--no-ff` in `master` gemergt.
**X** = 1 sobald alle Grundfunktionen nutzbar sind, **Y** = neues Feature, **Z** = Patch. Siehe [ROADMAP.md](../ROADMAP.md).
