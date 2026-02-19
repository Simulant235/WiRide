# WiRide - MVP Roadmap

## 📱 MVP 1 (Current) - UI & Navigation Only

### ✅ Functional Features:
1. **StartPage**
   - Navigation to HostPage
   - Navigation to ClientPage
   
2. **HostPage**
   - Display "You are Host" UI
   - Mute/Unmute button toggle (🎤 ↔️ 🔇)
   - Volume slider (UI only, not connected)
   - Back navigation ("Stop Hosting" button)
   
3. **ClientPage**
   - Display "You are Client" UI
   - Mute/Unmute button toggle (🎤 ↔️ 🔇)
   - Volume slider (UI only, not connected)
   - "Connect to Network" button (shows dialog: "Coming in MVP 2+")
   - Back navigation ("Disconnect" button)

### ❌ Not Yet Implemented (Coming in MVP 2+):
- Wi-Fi Hotspot creation
- Wi-Fi network scanning & connection
- Audio recording (microphone)
- Audio playback (speaker/headphones)
- UDP/Network communication
- Audio mixing (Mix-Minus)
- Client-Host connection
- Settings page

---

## 🚀 MVP 2 - Network & Audio Core

### Planned Features:
1. **WiFi Functionality**
   - ✅ Host: Start Wi-Fi Hotspot (Soft-AP)
   - ✅ Client: Scan for WiRide networks
   - ✅ Client: Connect to WiRide network
   
2. **Audio Capture & Playback**
   - ✅ Microphone recording (AudioRecord)
   - ✅ Speaker playback (AudioTrack)
   - ✅ Mute functionality (actual audio control)
   - ✅ Volume control (actual audio control)
   
3. **Network Communication**
   - ✅ UDP socket setup
   - ✅ Audio packet streaming
   - ✅ Client-Host handshake
   - ✅ Keepalive/heartbeat

4. **Audio Mixing**
   - ✅ Host receives audio from all clients
   - ✅ Mix-Minus implementation
   - ✅ Send mixed audio back to clients

---

## 🎯 MVP 3 - Optimization & Quality

### Planned Features:
1. **Audio Quality**
   - Opus codec integration (replace PCM)
   - Echo cancellation
   - Noise suppression
   - Adaptive buffer sizing
   
2. **Performance**
   - Latency monitoring & display
   - Network quality indicators
   - Automatic reconnection
   - Graceful degradation
   
3. **UI/UX**
   - Client list display (Host)
   - Connection strength indicators
   - Better error messages
   - Loading states

---

## ⚙️ MVP 4 - Settings & Customization

### Planned Features:
1. **Settings Page**
   - Host network name customization
   - Password customization
   - Audio codec selection (PCM/Opus)
   - Sample rate selection
   - Buffer size configuration
   
2. **Advanced Options**
   - Max clients limit
   - Auto-start hotspot
   - Push-to-talk mode
   - Voice activation threshold
   
3. **User Preferences**
   - Theme selection (Dark/Light)
   - Language selection
   - Default volume level
   - Notification settings

---

## 📊 Feature Status Overview

| Feature | MVP 1 | MVP 2 | MVP 3 | MVP 4 |
|---------|-------|-------|-------|-------|
| Navigation | ✅ | ✅ | ✅ | ✅ |
| Mute Button UI | ✅ | ✅ | ✅ | ✅ |
| WiFi Hotspot | ❌ | ✅ | ✅ | ✅ |
| Network Scanning | ❌ | ✅ | ✅ | ✅ |
| Audio Capture | ❌ | ✅ | ✅ | ✅ |
| Audio Playback | ❌ | ✅ | ✅ | ✅ |
| Mix-Minus | ❌ | ✅ | ✅ | ✅ |
| Opus Codec | ❌ | ❌ | ✅ | ✅ |
| Echo Cancel | ❌ | ❌ | ✅ | ✅ |
| Latency Monitor | ❌ | ❌ | ✅ | ✅ |
| Settings Page | ❌ | ❌ | ❌ | ✅ |
| Customization | ❌ | ❌ | ❌ | ✅ |

---

## 🔧 Technical Implementation Notes

### MVP 1 Implementation:
- All network/audio services are instantiated but not used
- Service methods are commented out with `/* Coming in MVP 2+ */`
- UI elements are fully functional (buttons, sliders, navigation)
- Mute toggle changes icon but doesn't affect audio (no audio yet)

### Code Structure:
```
✅ Models/ - Data structures ready
✅ Services/ - Interfaces defined, Android implementations ready
✅ Pages/ - UI complete, logic stubbed out
✅ Platform/Android - Service implementations complete but not used
```

### Migration Path to MVP 2:
1. Uncomment WiFi service initialization in `OnPageLoaded`
2. Uncomment network service setup
3. Uncomment audio service initialization
4. Test on physical Android device (WiFi Direct requires real hardware)
5. Add runtime permission requests
6. Test with 2-3 devices

---

## 📝 Developer Notes

### Current State (MVP 1):
- **Purpose**: UI/UX validation, navigation flow testing
- **Testing**: Can be tested on emulator
- **Demo**: Shows complete app flow without actual functionality

### Next Steps:
1. Get feedback on UI/UX design
2. Test navigation flow
3. Validate mute button icon toggle behavior
4. Prepare for MVP 2 hardware testing (need 2+ Android devices)

### Known Limitations:
- Settings button shows "Coming in MVP 2+" dialog
- "Connect to Network" shows "Coming in MVP 2+" dialog
- Volume slider is visual only
- No actual audio or network functionality

---

**Last Updated**: MVP 1 Implementation
**Status**: ✅ Complete - Ready for UI/UX Testing
