using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Tar;

namespace WiRide.Services;

// Plattformabhaengige Sprachsynthese mit einem Piper-Modell
public interface IPiperSynthesizer
{
    Task SpeakAsync(string modelDirectory, string text, float volume);
}

// Verwaltet die Piper-KI-Stimmen: welche Sprache welches Modell hat, Download und Speicherort
public static class PiperVoiceManager
{
    private const string BaseUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/";
    public const int ApproxDownloadMb = 64;

    // App-Sprache -> Piper-Modell (sherpa-onnx). Fehlt eine Sprache, gibt es keine KI-Stimme.
    private static readonly Dictionary<string, string> Models = new()
    {
        ["ar"] = "ar_JO-kareem-medium",
        ["cs"] = "cs_CZ-jirka-medium",
        ["da"] = "da_DK-talesyntese-medium",
        ["de"] = "de_DE-thorsten-medium",
        ["el"] = "el_GR-rapunzelina-low",
        ["en"] = "en_US-amy-medium",
        ["es"] = "es_ES-davefx-medium",
        ["fi"] = "fi_FI-harri-medium",
        ["fr"] = "fr_FR-siwis-medium",
        ["hi"] = "hi_IN-pratham-medium",
        ["id"] = "id_ID-news_tts-medium",
        ["it"] = "it_IT-paola-medium",
        ["nl"] = "nl_BE-nathalie-medium",
        ["no"] = "no_NO-talesyntese-medium",
        ["pl"] = "pl_PL-gosia-medium",
        ["pt"] = "pt_BR-faber-medium",
        ["ro"] = "ro_RO-mihai-medium",
        ["ru"] = "ru_RU-irina-medium",
        ["sv"] = "sv_SE-nst-medium",
        ["tr"] = "tr_TR-dfki-medium",
        ["uk"] = "uk_UA-ukrainian_tts-medium",
        ["vi"] = "vi_VN-vais1000-medium",
        ["zh"] = "zh_CN-huayan-medium",
    };

    private static string RootDirectory => Path.Combine(FileSystem.AppDataDirectory, "piper");

    public static bool IsSupported(string language) => Models.ContainsKey(language);

    public static string? ModelDirectory(string language) =>
        Models.TryGetValue(language, out var model) ? Path.Combine(RootDirectory, $"vits-piper-{model}") : null;

    public static bool IsInstalled(string language)
    {
        var dir = ModelDirectory(language);
        return dir != null && Directory.Exists(dir) && Directory.GetFiles(dir, "*.onnx").Length > 0;
    }

    public static long InstalledSizeMb()
    {
        if (!Directory.Exists(RootDirectory))
            return 0;
        return new DirectoryInfo(RootDirectory).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / (1024 * 1024);
    }

    public static void DeleteAll()
    {
        if (Directory.Exists(RootDirectory))
            Directory.Delete(RootDirectory, true);
    }

    // Laedt das Modell (tar.bz2) herunter und entpackt es. progress: 0..1
    public static async Task<bool> DownloadAsync(string language, IProgress<double> progress, CancellationToken token)
    {
        if (!Models.TryGetValue(language, out var model))
            return false;

        Directory.CreateDirectory(RootDirectory);
        var archive = Path.Combine(RootDirectory, $"{model}.tar.bz2");

        try
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
            using (var response = await http.GetAsync($"{BaseUrl}vits-piper-{model}.tar.bz2", HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? ApproxDownloadMb * 1024L * 1024L;

                await using var source = await response.Content.ReadAsStreamAsync(token);
                await using var target = File.Create(archive);

                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), token);
                    done += read;
                    progress.Report(Math.Min(0.95, done * 0.95 / total));
                }
            }

            // Entpacken (letzte 5 %)
            await Task.Run(() =>
            {
                using var file = File.OpenRead(archive);
                using var bzip = new BZip2InputStream(file);
                using var tar = TarArchive.CreateInputTarArchive(bzip, System.Text.Encoding.UTF8);
                tar.ExtractContents(RootDirectory);
            }, token);

            progress.Report(1);
            return IsInstalled(language);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Piper-Download fehlgeschlagen: {ex.Message}");
            var dir = ModelDirectory(language);
            if (dir != null && Directory.Exists(dir))
                Directory.Delete(dir, true);
            return false;
        }
        finally
        {
            if (File.Exists(archive))
                File.Delete(archive);
        }
    }
}
