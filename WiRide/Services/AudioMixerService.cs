namespace WiRide.Services;

// Mix-Minus-Mixer fuer PCM 16-bit Mono.
// Pro Tick wird von jeder Quelle ein Frame genommen und aufsummiert.
// Jeder Teilnehmer bekommt die Summe OHNE seine eigene Stimme (kein Echo).
public class AudioMixerService
{
    // Jitter-Buffer pro Quelle: max. 4 Frames (= 80 ms), aeltere werden verworfen
    private const int MaxBufferedFrames = 4;

    private readonly Dictionary<string, Queue<short[]>> _buffers = new();
    private readonly object _lock = new();

    public void AddClientAudio(string sourceId, byte[] pcm16)
    {
        var samples = new short[pcm16.Length / 2];
        Buffer.BlockCopy(pcm16, 0, samples, 0, samples.Length * 2);

        lock (_lock)
        {
            if (!_buffers.TryGetValue(sourceId, out var queue))
            {
                queue = new Queue<short[]>();
                _buffers[sourceId] = queue;
            }

            queue.Enqueue(samples);
            while (queue.Count > MaxBufferedFrames)
                queue.Dequeue();
        }
    }

    // Naechsten Frame aller Quellen holen. null = niemand spricht gerade.
    public MixFrame? NextFrame(int frameSamples)
    {
        lock (_lock)
        {
            var frame = new MixFrame(frameSamples);

            foreach (var (sourceId, queue) in _buffers)
            {
                if (queue.Count == 0)
                    continue;

                var samples = queue.Dequeue();
                frame.Parts[sourceId] = samples;

                int length = Math.Min(samples.Length, frameSamples);
                for (int i = 0; i < length; i++)
                    frame.Sum[i] += samples[i];
            }

            return frame.Parts.Count > 0 ? frame : null;
        }
    }

    // Mix-Minus: Summe aller Quellen ausser excludeId. null = nichts zu hoeren.
    public static byte[]? MixMinus(MixFrame frame, string excludeId)
    {
        frame.Parts.TryGetValue(excludeId, out var own);
        if (frame.Parts.Count - (own != null ? 1 : 0) == 0)
            return null;

        var result = new byte[frame.Sum.Length * 2];
        for (int i = 0; i < frame.Sum.Length; i++)
        {
            int value = frame.Sum[i];
            if (own != null && i < own.Length)
                value -= own[i];

            short clamped = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
            result[i * 2] = (byte)clamped;
            result[i * 2 + 1] = (byte)(clamped >> 8);
        }

        return result;
    }

    public void RemoveClient(string sourceId)
    {
        lock (_lock)
        {
            _buffers.Remove(sourceId);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _buffers.Clear();
        }
    }
}

public sealed class MixFrame
{
    public MixFrame(int samples) => Sum = new int[samples];

    public int[] Sum { get; }
    public Dictionary<string, short[]> Parts { get; } = new();
}
