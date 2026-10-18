namespace WiRide.Services;

// Mix-Minus-Mixer fuer PCM 16-bit Mono.
// Pro Tick wird von jeder Quelle ein Frame genommen. Jeder Zuhoerer bekommt alle Quellen
// OHNE seine eigene Stimme (kein Echo), optional normalisiert nach seinen eigenen Wuenschen.
public class AudioMixerService
{
    private const double TargetRms = 3000;   // Ziel-Lautstaerke fuer die Normalisierung
    private const double SilenceRms = 300;   // darunter ist es Stille/Rauschen -> Verstaerkung nicht anpassen
    private const double SpeechRms = 600;    // ab hier spricht jemand -> Musik wird abgesenkt
    private const double MusicDuckGain = 0.2;

    public const string MusicSuffix = ":music";
    public static string MusicId(string sourceId) => sourceId + MusicSuffix;
    private static bool IsMusic(string sourceId) => sourceId.EndsWith(MusicSuffix, StringComparison.Ordinal);

    private double _musicGain = 1.0;
    private int _speechHold;

    private readonly Dictionary<string, Queue<short[]>> _buffers = new();
    private readonly Dictionary<string, double> _sourceGain = new();
    private readonly object _lock = new();

    // Jitter-Buffer pro Quelle (Frames), aeltere werden verworfen
    public int MaxBufferedFrames { get; set; } = 4;

    public void AddClientAudio(string sourceId, byte[] pcm16)
    {
        var samples = AudioProcessor.ToSamples(pcm16);

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
                frame.NormalizeGain[sourceId] = IsMusic(sourceId) ? 1.0 : UpdateGain(sourceId, samples);

                int length = Math.Min(samples.Length, frameSamples);
                for (int i = 0; i < length; i++)
                    frame.Sum[i] += samples[i];
            }

            // Musik absenken, solange jemand spricht (bei allen Zuhoerern)
            bool speech = frame.Parts.Any(p => !IsMusic(p.Key) && AudioProcessor.Rms(p.Value) > SpeechRms);
            _speechHold = speech ? 25 : Math.Max(0, _speechHold - 1); // 500 ms nachhalten
            double target = _speechHold > 0 ? MusicDuckGain : 1.0;
            _musicGain = _musicGain * 0.7 + target * 0.3;
            frame.MusicGain = _musicGain;

            return frame.Parts.Count > 0 ? frame : null;
        }
    }

    // Weich nachgefuehrte Verstaerkung, die diese Quelle auf Ziel-Lautstaerke bringen wuerde
    private double UpdateGain(string sourceId, short[] samples)
    {
        double current = _sourceGain.GetValueOrDefault(sourceId, 1.0);
        double rms = AudioProcessor.Rms(samples);

        if (rms > SilenceRms)
        {
            double target = Math.Clamp(TargetRms / rms, 0.25, 4.0);
            current = current * 0.85 + target * 0.15;
            _sourceGain[sourceId] = current;
        }

        return current;
    }

    // Mix-Minus fuer einen Zuhoerer. normalizeStrength 0 = aus, 1 = volle Normalisierung.
    // null = nichts zu hoeren.
    public static byte[]? MixMinus(MixFrame frame, string excludeId, double normalizeStrength = 0)
    {
        frame.Parts.TryGetValue(excludeId, out var own);
        string ownMusic = MusicId(excludeId);
        bool hasMusic = frame.Parts.Keys.Any(IsMusic);

        if (!frame.Parts.Keys.Any(k => k != excludeId && k != ownMusic))
            return null;

        var result = new short[frame.Sum.Length];

        if (normalizeStrength <= 0 && !hasMusic)
        {
            // Schneller Weg: Summe minus eigene Stimme
            for (int i = 0; i < result.Length; i++)
            {
                int value = frame.Sum[i];
                if (own != null && i < own.Length)
                    value -= own[i];
                result[i] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
            }
        }
        else
        {
            var mix = new double[result.Length];
            foreach (var (sourceId, samples) in frame.Parts)
            {
                if (sourceId == excludeId || sourceId == ownMusic)
                    continue;

                double gain = IsMusic(sourceId)
                    ? frame.MusicGain
                    : Math.Pow(frame.NormalizeGain[sourceId], normalizeStrength);
                int length = Math.Min(samples.Length, mix.Length);
                for (int i = 0; i < length; i++)
                    mix[i] += samples[i] * gain;
            }

            for (int i = 0; i < result.Length; i++)
                result[i] = (short)Math.Clamp(mix[i], short.MinValue, short.MaxValue);
        }

        return AudioProcessor.ToBytes(result);
    }

    public void RemoveClient(string sourceId)
    {
        lock (_lock)
        {
            _buffers.Remove(sourceId);
            _buffers.Remove(MusicId(sourceId));
            _sourceGain.Remove(sourceId);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _buffers.Clear();
            _sourceGain.Clear();
        }
    }
}

public sealed class MixFrame
{
    public MixFrame(int samples) => Sum = new int[samples];

    public int[] Sum { get; }
    public Dictionary<string, short[]> Parts { get; } = new();
    public Dictionary<string, double> NormalizeGain { get; } = new();
    public double MusicGain { get; set; } = 1.0;
}
