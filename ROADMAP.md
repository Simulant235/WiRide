# WiRide - Roadmap & Changelog

## Changelog

| Version | Inhalt |
|---|---|
| v0.1.0 | UI & Navigation |
| v0.1.1 | Runtime-Permissions |
| v0.3.0 | Audio-Test & UI-Cleanup |
| v0.4.0 | UDP-Netzwerk, Audio-Streaming, Mix-Minus, Latenz, Auto-Reconnect |
| v0.5.0 | WiFi-Hotspot (LocalOnlyHotspot), WLAN-Scan und -Verbindung |
| v0.5.1 | Veraltete MAUI-APIs ersetzt |
| v0.6.0 | Einstellungen-Seite |
| v0.7.0 | Opus-Codec |
| v0.8.0 | Hintergrundbetrieb (Foreground-Service) |
| v0.8.1 | Dokumentation aktualisiert |
| v1.0.0 | Erste nutzbare Version: alle Grundfunktionen fertig |
| v1.1.0 | Eigenes Hotspot-Passwort, QR-Code-Beitritt, neues Design, Einstellungen erweitert, Stop-Fix |

## Praxistest v1.0.0

Alle Grundfunktionen sind implementiert. Offene Praxistests auf echten Geräten:

- [ ] Host + 2 Clients auf echten Android-Geräten (Hotspot, Suche, Audio, Mix-Minus)
- [ ] Latenz messen (Ziel: < 150 ms Ende-zu-Ende)
- [ ] Hintergrundbetrieb mit gesperrtem Bildschirm (mind. 30 min)
- [ ] Reconnect bei Funkloch (Abstand vergrößern und zurückkommen)
- [ ] Bluetooth-Headset im Helm

## Ideen nach v1.0.0

- iOS-Unterstützung (AVAudioEngine; Host nur über bestehendes WLAN, da iOS keinen App-Hotspot erlaubt)
- Verschlüsselung der Audio-Pakete
- Adaptive Jitter-Buffer / Packet-Loss-Concealment (Opus FEC)
- Voice-Activity-Detection (nur senden, wenn gesprochen wird)
- Lautstärke pro Rider beim Host
