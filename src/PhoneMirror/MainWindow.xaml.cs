using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace PhoneMirror;
public partial class MainWindow : Window
{
    private readonly Receiver receiver = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool checking, busy;
    private MouseWindow? mouseWindow;
    private PhoneWindow? phoneWindow;
    public MainWindow()
    {
        InitializeComponent();
        timer.Tick += async (_, _) => await Refresh();
        timer.Start();
        Loaded += async (_, _) => await Refresh();
        Closing += (_, _) => { timer.Stop(); mouseWindow?.Close(); phoneWindow?.Close(); try { receiver.Dispose(); } catch { /* Never prevent closing the window. */ } };
    }
    public async Task StartReceiver()
    {
        if (busy) return;
        busy = true; StartButton.IsEnabled = StopButton.IsEnabled = false;
        ErrorText.Text = ""; StatusText.Text = "Запускаю приёмник…";
        var profile = ProfileBox.SelectedIndex;
        var width = profile switch { 1 => 1280, 3 => 2560, 4 => 3840, _ => 1920 };
        var height = profile switch { 1 => 720, 3 => 1440, 4 => 2340, _ => 1080 };
        var quality = new Quality(width, height, profile is 2 or 4 ? 30 : 60, DecoderBox.SelectedIndex == 0, AudioBox.IsChecked == true, SyncBox.IsChecked == true, profile >= 3);
        try
        {
            if (!System.IO.File.Exists(receiver.EnginePath))
            {
                var download = MessageBox.Show(this,
                    "Для трансляции нужен открытый приёмник UxPlay. Скачать его Windows-сборку с github.com/leapbtw/uxplay-windows (около 110 МБ)?\n\nКомпонент распространяется отдельно под GPL-3.0. Контрольная сумма будет проверена перед распаковкой. При первом запуске компонент может запросить установку службы обнаружения с правами администратора.",
                    "Компонент приёма экрана", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (download != MessageBoxResult.Yes) { StatusText.Text = "Загрузка приёмника отменена"; return; }
                await EngineInstaller.Install(receiver.Root, new Progress<string>(text => StatusText.Text = text));
            }
            await receiver.Start(quality);
            StatusText.Text = "Выбери Potok на iPhone";
            ProfileText.Text = $"Запрошено {quality.Width} × {quality.Height} · {quality.Fps} кадров/с · " + (quality.Gpu ? "D3D11" : "совместимый CPU-декодер");
            StartButton.Content = "Применить и переподключить";
            OpenPhone();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Не удалось запустить"; ErrorText.Text = ex.Message;
            System.IO.File.AppendAllText(System.IO.Path.Combine(receiver.LogDirectory, "application.log"), ex + Environment.NewLine);
        }
        finally { busy = false; StartButton.IsEnabled = true; StopButton.IsEnabled = receiver.ProcessId != null; }
    }
    private async void Start_Click(object sender, RoutedEventArgs e) => await StartReceiver();
    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        try { receiver.Stop(); StatusText.Text = "Трансляция остановлена"; RouteText.Text = "Соединение закрыто"; StopButton.IsEnabled = false; StartButton.Content = "Начать трансляцию"; }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }
    private void Logs_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("explorer.exe", receiver.LogDirectory) { UseShellExecute = true });
    private async Task Refresh()
    {
        if (checking || busy) return;
        checking = true;
        try
        {
            var network = ConnectionInfo.Usb(); NetworkText.Text = network.Description;
            if (receiver.VideoError != null)
            {
                if (receiver.ActiveGpu)
                {
                    DecoderBox.SelectedIndex = 1;
                    await StartReceiver();
                    ErrorText.Text = "GPU не принял поток. Включён совместимый декодер. На iPhone заново выбери Potok.";
                }
                else
                {
                    StatusText.Text = "Ошибка видеопотока";
                    ErrorText.Text = receiver.VideoError + " Журнал доступен в диагностике.";
                }
                return;
            }
            var processId = receiver.ProcessId;
            if (processId == null) return;
            using var netstat = Process.Start(new ProcessStartInfo("netstat.exe", "-ano -p tcp")
            { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true })!;
            var output = await netstat.StandardOutput.ReadToEndAsync();
            await netstat.WaitForExitAsync();
            var routes = ConnectionInfo.Routes();
            NetworkText.Text = string.Join("\n", routes.Select(r => $"{(r.Usb ? "USB" : "Локальная сеть")} · {r.Address} ({r.Adapter})"));
            var route = ConnectionInfo.FindRoute(output, processId.Value, routes);
            RouteText.Text = route == null ? "Ожидаю подключение · на iPhone выбери Potok" : $"Путь соединения: {(route.Usb ? "USB" : "локальная сеть / Wi-Fi")} · {route.Address}";
            MetricsText.Text = receiver.Metrics.Summary;
            StatusText.Text = route != null ? "iPhone подключён" : "Приёмник готов";
        }
        catch (Exception ex) { ErrorText.Text = "Диагностика: " + ex.Message; }
        finally { checking = false; }
    }
    private void Mouse_Click(object sender, RoutedEventArgs e)
    {
        if (mouseWindow == null)
        {
            mouseWindow = new MouseWindow { Owner = this };
            mouseWindow.Closed += (_, _) => mouseWindow = null;
            mouseWindow.Show();
        }
        else mouseWindow.Activate();
    }
    private void Phone_Click(object sender, RoutedEventArgs e) => OpenPhone();
    private void OpenPhone()
    {
        if (phoneWindow == null)
        {
            phoneWindow = new PhoneWindow(receiver);
            phoneWindow.Closed += (_, _) => phoneWindow = null;
            phoneWindow.Show();
        }
        else { phoneWindow.WindowState = WindowState.Normal; phoneWindow.Activate(); }
    }
}
