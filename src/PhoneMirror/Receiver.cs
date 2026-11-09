using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;

namespace PhoneMirror;

public record Quality(int Width, int Height, int Fps, bool Gpu, bool Audio, bool Sync, bool Hevc = false)
{
    public string Arguments => $"-n Potok -nh -s {Width}x{Height}@{Fps} -fps {Fps} " +
        (Gpu ? "-vd d3d11h264dec -vc d3d11convert -vs d3d11videosink " : "-avdec ") +
        (Hevc ? "-h265 " : "") + (Audio ? "-al 0.1 " : "-as 0 ") + (Sync ? "-vsync " : "-vsync no ") + "-FPSdata";
}

public sealed class Receiver : IDisposable
{
    public string Root { get; }
    public string LogDirectory => Path.Combine(Root, "logs");
    public string EnginePath => Path.Combine(Root, "tools", "uxplay", "uxplay-windows.exe");
    private string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "leapbtw", "uxplay-windows", "arguments.txt");
    private Process? process;
    public int? ProcessId => process is { HasExited: false } ? process.Id : null;
    private string? previousConfig, writtenConfig;
    private bool configExisted;
    private string? sessionLog;
    public string? VideoError { get; private set; }
    public StreamMetrics Metrics { get; private set; } = new();
    private readonly System.Text.StringBuilder metricsXml = new();
    private bool readingMetrics;
    public bool ActiveGpu { get; private set; }
    public string? DeviceModel { get; private set; }
    public static bool IsVideoFailure(string line) =>
        line.Contains("not-negotiated", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("GStreamer error (video)", StringComparison.OrdinalIgnoreCase);

    public Receiver()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "potok.json"))) directory = directory.Parent;
        Root = directory?.FullName ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(LogDirectory);
    }

    public async Task Start(Quality quality)
    {
        VideoError = null;
        lock (metricsXml) { Metrics = new(); readingMetrics = false; metricsXml.Clear(); }
        ActiveGpu = quality.Gpu;
        if (!File.Exists(EnginePath)) throw new FileNotFoundException("Не найден tools/uxplay/uxplay-windows.exe. См. README.md.");
        var machineConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "uxplay-windows", "arguments.txt");
        if (File.Exists(machineConfig)) throw new InvalidOperationException("Настройки приёмника заданы администратором в ProgramData. Профиль приложения не будет применён.");
        // Only take over this project's receiver, never another installed copy.
        foreach (var other in Process.GetProcessesByName("uxplay-windows"))
        {
            using (other)
            {
                if (!string.Equals(other.MainModule?.FileName, EnginePath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Уже работает другая копия UxPlay. Закрой её перед запуском.");
            }
        }
        StopProcess();
        foreach (var other in Process.GetProcessesByName("uxplay-windows"))
        {
            using (other)
            {
                if (string.Equals(other.MainModule?.FileName, EnginePath, StringComparison.OrdinalIgnoreCase))
                { other.Kill(true); await other.WaitForExitAsync(); }
            }
        }
        if (writtenConfig == null)
        {
            configExisted = File.Exists(ConfigPath);
            previousConfig = configExisted ? File.ReadAllText(ConfigPath) : "";
            File.WriteAllText(Path.Combine(LogDirectory, "receiver-config-before.txt"), previousConfig);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        writtenConfig = quality.Arguments;
        File.WriteAllText(ConfigPath, writtenConfig);
        sessionLog = Path.Combine(LogDirectory, $"receiver-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
        File.WriteAllText(sessionLog, $"Requested: {writtenConfig}{Environment.NewLine}");
        var start = new ProcessStartInfo(EnginePath)
        {
            WorkingDirectory = Path.GetDirectoryName(EnginePath)!, UseShellExecute = false,
            RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true
        };
        start.Environment["GST_DEBUG"] = "2,h264parse:4,h265parse:4";
        start.Environment["GST_DEBUG_NO_COLOR"] = "1";
        process = new Process { StartInfo = start };
        process.OutputDataReceived += Capture;
        process.ErrorDataReceived += Capture;
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        await Task.Delay(1800);
        if (process.HasExited) throw new InvalidOperationException($"Приёмник завершился: {process.ExitCode}. Проверь журнал. Для начала попробуй автоматическое декодирование.");
    }

    private readonly object logLock = new();
    private void Capture(object sender, DataReceivedEventArgs e)
    {
        if (e.Data == null || sessionLog == null) return;
        if (IsVideoFailure(e.Data)) VideoError = "Декодер не смог принять видеопоток.";
        var deviceMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"\((iPhone\d+,\d+)\)");
        if (deviceMatch.Success) DeviceModel = deviceMatch.Groups[1].Value;
        lock (metricsXml)
        {
            var size = StreamMetrics.FindSize(e.Data);
            if (size != null) Metrics = Metrics with { Size = size };
            if (e.Data.Contains("<?xml")) { readingMetrics = true; metricsXml.Clear(); }
            if (readingMetrics)
            {
                metricsXml.AppendLine(e.Data);
                if (metricsXml.Length > 65536) { readingMetrics = false; metricsXml.Clear(); }
                else if (e.Data.Contains("</plist>"))
                {
                    readingMetrics = false;
                    try { Metrics = StreamMetrics.Parse(metricsXml.ToString(), Metrics); } catch (System.Xml.XmlException) { }
                }
            }
        }
        lock (logLock) { try { File.AppendAllText(sessionLog, e.Data + Environment.NewLine); } catch (IOException) { } }
    }
    private void StopProcess()
    {
        if (process == null) return;
        if (!process.HasExited) { process.Kill(true); process.WaitForExit(3000); }
        process.Dispose(); process = null;
    }
    public void Stop()
    {
        StopProcess();
        if (writtenConfig != null && File.Exists(ConfigPath) && File.ReadAllText(ConfigPath) == writtenConfig)
        {
            if (configExisted) File.WriteAllText(ConfigPath, previousConfig);
            else File.Delete(ConfigPath);
        }
        writtenConfig = null;
    }
    public void Dispose() => Stop();
}

public static class ConnectionInfo
{
    public record Route(string Address, string Adapter, bool Usb);
    public static Route[] Routes() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses.Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254."))
            .Select(a => new Route(a.Address.ToString(), n.Name, n.Description.Contains("Apple", StringComparison.OrdinalIgnoreCase)))).ToArray();
    public static Route? FindRoute(string netstat, int processId, Route[] routes) => routes.FirstOrDefault(r => HasUsbConnection(netstat, processId, r.Address));
    public static (string Description, string? Ip) Usb()
    {
        var adapter = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n =>
            n.Description.Contains("Apple", StringComparison.OrdinalIgnoreCase) && n.OperationalStatus == OperationalStatus.Up);
        var ip = adapter?.GetIPProperties().UnicastAddresses.FirstOrDefault(a =>
            a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254."))?.Address.ToString();
        return (adapter == null ? "USB-сеть не найдена · можно подключиться по Wi-Fi" : ip == null ? "USB подключён · ожидаю IP-адрес" : $"USB подключён · {ip}", ip);
    }

    public static bool HasUsbConnection(string netstat, int processId, string ip) => netstat.Split('\n').Any(line =>
    {
        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return fields.Length == 5 && fields[0] == "TCP" && fields[1].StartsWith(ip + ":", StringComparison.Ordinal) &&
            fields[3] == "ESTABLISHED" && fields[4] == processId.ToString() &&
            fields[2] != fields[1] && !fields[2].StartsWith("127.");
    });
}
