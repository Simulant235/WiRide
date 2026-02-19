using WiRide.Models;

namespace WiRide.Services;

public class AudioMixerService
{
    private readonly Dictionary<string, Queue<byte[]>> _clientAudioBuffers = new();
    private readonly object _lockObject = new();

    public void AddClientAudio(string clientId, byte[] audioData)
    {
        lock (_lockObject)
        {
            if (!_clientAudioBuffers.ContainsKey(clientId))
            {
                _clientAudioBuffers[clientId] = new Queue<byte[]>();
            }

            _clientAudioBuffers[clientId].Enqueue(audioData);

            if (_clientAudioBuffers[clientId].Count > 10)
            {
                _clientAudioBuffers[clientId].Dequeue();
            }
        }
    }

    public byte[] MixAudioForClient(string excludeClientId, int bufferSize = 1024)
    {
        lock (_lockObject)
        {
            var mixedAudio = new float[bufferSize];
            int clientCount = 0;

            foreach (var kvp in _clientAudioBuffers)
            {
                // Mix-Minus: Audio dieses Clients ausschliessen
                if (kvp.Key == excludeClientId)
                    continue;

                if (kvp.Value.Count > 0)
                {
                    var audioData = kvp.Value.Dequeue();

                    // Bytes zu Float-Samples konvertieren und mischen
                    for (int i = 0; i < Math.Min(audioData.Length, bufferSize); i++)
                    {
                        // Byte zu Float konvertieren (-1.0 bis 1.0)
                        float sample = (audioData[i] - 128) / 128f;
                        mixedAudio[i] += sample;
                    }
                    clientCount++;
                }
            }

            // Gemischtes Audio normalisieren
            if (clientCount > 0)
            {
                for (int i = 0; i < mixedAudio.Length; i++)
                {
                    mixedAudio[i] /= clientCount;
                }
            }

            // Zurueck zu Bytes konvertieren
            var result = new byte[bufferSize];
            for (int i = 0; i < mixedAudio.Length; i++)
            {
                // Begrenzen und zu Byte konvertieren
                float clamped = Math.Clamp(mixedAudio[i], -1f, 1f);
                result[i] = (byte)((clamped * 128) + 128);
            }

            return result;
        }
    }

    public void RemoveClient(string clientId)
    {
        lock (_lockObject)
        {
            _clientAudioBuffers.Remove(clientId);
        }
    }

    public void Clear()
    {
        lock (_lockObject)
        {
            _clientAudioBuffers.Clear();
        }
    }
}
