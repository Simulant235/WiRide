using System.Collections.Concurrent;
using Concentus;
using Concentus.Enums;
using WiRide.Models;

namespace WiRide.Services;

// Opus-Kompression (Concentus, reines C#). Spart ca. 90% Bandbreite gegenueber PCM.
// Opus ist zustandsbehaftet: ein Encoder pro Ziel-Stream und ein Decoder pro Quelle.
public class AudioCodecService
{
    private const int OpusBitrate = 24000;
    private const int MaxOpusPacketBytes = 1275;

    private readonly ConcurrentDictionary<string, IOpusEncoder> _encoders = new();
    private readonly ConcurrentDictionary<string, IOpusDecoder> _decoders = new();

    public AudioCodecType OutgoingCodec => AppSettings.UseOpus ? AudioCodecType.Opus : AudioCodecType.Pcm16;

    public byte[] Encode(string streamId, byte[] pcm16)
    {
        if (OutgoingCodec == AudioCodecType.Pcm16)
            return pcm16;

        int frameSamples = AppSettings.FrameSamples;
        var samples = new short[frameSamples]; // fehlende Samples bleiben Stille
        Buffer.BlockCopy(pcm16, 0, samples, 0, Math.Min(pcm16.Length, frameSamples * 2));

        var encoder = _encoders.GetOrAdd(streamId, _ =>
        {
            var e = OpusCodecFactory.CreateEncoder(AppSettings.SampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            e.Bitrate = OpusBitrate;
            return e;
        });

        var output = new byte[MaxOpusPacketBytes];
        int length = encoder.Encode(samples, frameSamples, output, output.Length);
        return output.AsSpan(0, length).ToArray();
    }

    // null = Paket nicht dekodierbar (wird verworfen)
    public byte[]? Decode(string sourceId, AudioPacket packet)
    {
        if (packet.Codec == AudioCodecType.Pcm16)
            return packet.AudioData;

        if (packet.Codec != AudioCodecType.Opus)
            return null;

        try
        {
            var decoder = _decoders.GetOrAdd(sourceId, _ => OpusCodecFactory.CreateDecoder(AppSettings.SampleRate, 1));

            // Platz fuer bis zu 120 ms (groesster Opus-Frame)
            var samples = new short[AppSettings.SampleRate * 120 / 1000];
            int decoded = decoder.Decode(packet.AudioData, samples, samples.Length, false);

            var pcm = new byte[decoded * 2];
            Buffer.BlockCopy(samples, 0, pcm, 0, pcm.Length);
            return pcm;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Opus-Dekodierung fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    public void Remove(string streamId)
    {
        _encoders.TryRemove(streamId, out _);
        _decoders.TryRemove(streamId, out _);
    }

    public void Clear()
    {
        _encoders.Clear();
        _decoders.Clear();
    }
}
