using System.Media;
using System.Text;

namespace MultiplayerLauncher;

internal enum SoundType { Click, Select, Success, Launch, Error }

internal static class SoundFX
{
    public static void Play(SoundType type) => Task.Run(() =>
    {
        try
        {
            byte[] wav = type switch
            {
                SoundType.Click   => Build([(620,  55, 0.09f)]),
                SoundType.Select  => Build([(900,  38, 0.08f), (1120, 55, 0.08f)]),
                SoundType.Success => Build([(660,  65, 0.08f), (880,  65, 0.08f), (1100, 95, 0.09f)]),
                SoundType.Launch  => Build([(440,  45, 0.07f), (660,  45, 0.07f), (880,  85, 0.10f)]),
                SoundType.Error   => Build([(320,  75, 0.09f), (260, 100, 0.09f)]),
                _                 => Array.Empty<byte>()
            };
            if (wav.Length == 0) return;
            using var ms     = new MemoryStream(wav);
            using var player = new SoundPlayer(ms);
            player.PlaySync();
        }
        catch { /* never crash from sound */ }
    });

    // ── WAV builder ────────────────────────────────────────────────────────────

    private static byte[] Build((int hz, int ms, float vol)[] notes)
    {
        const int rate  = 44100;
        const int bits  = 16;
        const int bytes = bits / 8;

        var samples = new List<short>();

        foreach (var (hz, ms, vol) in notes)
        {
            int n = rate * ms / 1000;
            for (int i = 0; i < n; i++)
            {
                double t   = (double)i / rate;
                double env = i < n * 0.10 ? i / (n * 0.10)
                           : i > n * 0.75 ? 1.0 - (i - n * 0.75) / (n * 0.25)
                           : 1.0;
                double v = Math.Sin(2 * Math.PI * hz * t) * env * vol * short.MaxValue;
                samples.Add((short)Math.Clamp(v, short.MinValue, short.MaxValue));
            }
            // tiny gap between notes
            for (int i = 0; i < rate * 5 / 1000; i++) samples.Add(0);
        }

        int dataSize  = samples.Count * bytes;
        int byteRate  = rate * bytes;

        using var stream = new MemoryStream();
        using var w      = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataSize);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);               // subchunk1 size
        w.Write((short)1);         // PCM
        w.Write((short)1);         // mono
        w.Write(rate);             // sample rate
        w.Write(byteRate);         // byte rate
        w.Write((short)bytes);     // block align
        w.Write((short)bits);      // bits per sample
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);
        foreach (short s in samples) w.Write(s);

        return stream.ToArray();
    }
}
