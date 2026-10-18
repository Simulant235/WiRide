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
| v1.2.0 | Einstellungs-Kategorien, Sprachsystem, helles/dunkles Design, Schriftgröße |
| v1.3.0 | Latenz-Modus, Rauschunterdrückung, Normalisierung, Sprachaktivierung |
| v1.4.0 | Sprachansagen, KI-Stimme (Piper), Schnell-Ansagen, Ducking, Musik teilen |
| v1.5.0 | Schnellstart, Passwort merken, Akku/Signal pro Rider, Lautstärke nach Tempo |
| v1.6.0 | Home-Widget, Mini-Fenster, Mikrofon-Test, Headset-Warnung, Einführung, Feedback |
| v1.7.0 | 27 Sprachen, Rechts-nach-links-Layout für Arabisch |

## Praxistest v1.0.0

Alle Grundfunktionen sind implementiert. Offene Praxistests auf echten Geräten:

- [ ] Host + 2 Clients auf echten Android-Geräten (Hotspot, Suche, Audio, Mix-Minus)
- [ ] Latenz messen (Ziel: < 150 ms Ende-zu-Ende)
- [ ] Hintergrundbetrieb mit gesperrtem Bildschirm (mind. 30 min)
- [ ] Reconnect bei Funkloch (Abstand vergrößern und zurückkommen)
- [ ] Bluetooth-Headset im Helm

## Ideen für später

- iOS-Unterstützung (AVAudioEngine; Host nur über den persönlichen Hotspot)
- Verschlüsselung der Audio-Pakete
- Packet-Loss-Concealment (Opus FEC)
- Mehr Reichweite (Relay über einen zweiten Host)
- Übersetzungen durch Muttersprachler prüfen lassen
