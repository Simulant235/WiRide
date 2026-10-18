namespace WiRide.Services;

// Bearbeitet das eigene Mikrofon-Signal, bevor es gesendet wird (betrifft nur dieses Handy):
// - Rauschunterdrueckung: Hochpass gegen Wind/Motor-Brummen + Rauschsperre (Noise Gate)
// - Sprachaktivierung (VAD): nur senden, wenn gesprochen wird
public class AudioProcessor
{
    private const int VadHangoverFrames = 15; // 300 ms nachlaufen lassen, damit Wortenden nicht abgeschnitten werden

    private double _noiseFloor = 400;
    private float _hpPrevIn;
    private float _hpPrevOut;
    private float _gateGain = 1f;
    private int _hangover;

    public void Reset()
    {
        _noiseFloor = 400;
        _hpPrevIn = _hpPrevOut = 0;
        _gateGain = 1f;
        _hangover = 0;
    }

    // false = Frame nicht senden (Sprachaktivierung: niemand spricht)
    public bool Process(short[] samples, int sampleRate)
    {
        double rms = Rms(samples);
        UpdateNoiseFloor(rms);

        if (AppSettings.NoiseReductionEnabled)
        {
            double strength = AppSettings.NoiseReductionStrength;
            HighPass(samples, sampleRate, 60 + 120 * strength);
            Gate(samples, rms, strength);
        }

        if (!AppSettings.VoiceActivationEnabled)
            return true;

        // Hohe Empfindlichkeit = niedrige Schwelle
        double threshold = _noiseFloor * (1.8 + 4.0 * (1 - AppSettings.VoiceActivationSensitivity));
        if (rms > Math.Max(threshold, 250))
            _hangover = VadHangoverFrames;
        else if (_hangover > 0)
            _hangover--;

        return _hangover > 0;
    }

    private void UpdateNoiseFloor(double rms)
    {
        // Faellt schnell, steigt langsam: folgt dem Grundrauschen, nicht der Stimme
        _noiseFloor = rms < _noiseFloor
            ? _noiseFloor * 0.9 + rms * 0.1
            : _noiseFloor * 0.997 + rms * 0.003;
        _noiseFloor = Math.Clamp(_noiseFloor, 50, 8000);
    }

    private void HighPass(short[] samples, int sampleRate, double cutoffHz)
    {
        double rc = 1.0 / (2 * Math.PI * cutoffHz);
        double dt = 1.0 / sampleRate;
        float a = (float)(rc / (rc + dt));

        for (int i = 0; i < samples.Length; i++)
        {
            float x = samples[i];
            float y = a * (_hpPrevOut + x - _hpPrevIn);
            _hpPrevIn = x;
            _hpPrevOut = y;
            samples[i] = (short)Math.Clamp(y, short.MinValue, short.MaxValue);
        }
    }

    private void Gate(short[] samples, double rms, double strength)
    {
        double threshold = _noiseFloor * (1.5 + 2.5 * strength);
        float target = rms < threshold ? (float)(1 - 0.9 * strength) : 1f;

        // Verstaerkung ueber den Frame weich ueberblenden (keine Klicks)
        float start = _gateGain;
        for (int i = 0; i < samples.Length; i++)
        {
            float gain = start + (target - start) * i / samples.Length;
            samples[i] = (short)(samples[i] * gain);
        }
        _gateGain = target;
    }

    public static double Rms(short[] samples)
    {
        if (samples.Length == 0)
            return 0;

        double sum = 0;
        foreach (var s in samples)
            sum += (double)s * s;
        return Math.Sqrt(sum / samples.Length);
    }

    public static short[] ToSamples(byte[] pcm)
    {
        var samples = new short[pcm.Length / 2];
        Buffer.BlockCopy(pcm, 0, samples, 0, samples.Length * 2);
        return samples;
    }

    public static byte[] ToBytes(short[] samples)
    {
        var pcm = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, pcm, 0, pcm.Length);
        return pcm;
    }
}
