using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace PhoneMirror;
public static class EngineInstaller
{
    public const string Version = "2.0.0.1736";
    public const string DownloadUrl = "https://github.com/leapbtw/uxplay-windows/releases/download/2.0.0.1736/uxplay-windows.zip";
    public const string Sha256 = "9D3A51C15FC9DB857351195E7EB7BBB21700D9AE25D936A54BCF8536B62CCA18";
    public static async Task Install(string root, IProgress<string> progress, CancellationToken cancellationToken = default)
    {
        var tools = Path.Combine(root, "tools"); Directory.CreateDirectory(tools);
        var destination = Path.Combine(tools, "uxplay");
        if (File.Exists(Path.Combine(destination, "uxplay-windows.exe"))) return;
        if (Directory.Exists(destination)) throw new IOException("Папка tools/uxplay уже существует, но приёмник не найден. Перемести повреждённую папку и повтори загрузку.");
        var staging = Path.Combine(tools, ".download-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
        try
        {
            var archive = Path.Combine(staging, "receiver.zip");
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Potok/0.3.2");
            using var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            const long maximumBytes = 150L * 1024 * 1024;
            if (total > maximumBytes) throw new IOException("Размер загружаемого файла превышает ожидаемый.");
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var file = File.Create(archive))
            {
                var buffer = new byte[128 * 1024]; long received = 0; long lastReport = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    received += count;
                    if (received > maximumBytes) throw new IOException("Размер загружаемого файла превышает ожидаемый.");
                    await file.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    if (received - lastReport >= 1024 * 1024)
                    {
                        lastReport = received;
                        progress.Report($"Загружаю приёмник: {received / (1024 * 1024)} МБ" + (total.HasValue ? $" / {total / (1024 * 1024)} МБ" : ""));
                    }
                }
            }
            progress.Report("Проверяю и распаковываю приёмник…");
            await VerifyArchive(archive, cancellationToken);
            var extracted = Path.Combine(staging, "extracted");
            await Task.Run(() => ZipFile.ExtractToDirectory(archive, extracted), cancellationToken);
            if (!File.Exists(Path.Combine(extracted, "uxplay-windows.exe"))) throw new IOException("В архиве не найден приёмник.");
            Directory.Move(extracted, destination);
        }
        finally
        {
            // Delete only the unique scratch directory created by this call, under our tools directory.
            var fullPath = Path.GetFullPath(staging);
            if (fullPath.StartsWith(Path.GetFullPath(tools) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }
    }
    public static async Task VerifyArchive(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        if (!string.Equals(actual, Sha256, StringComparison.Ordinal)) throw new IOException("Контрольная сумма приёмника не совпала. Файл не будет запущен.");
    }
}
