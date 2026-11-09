using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PhoneMirror;
public sealed class MouseWindow : Window
{
    private readonly BleMouse mouse = new();
    private readonly TextBlock status = new() { Text = "Нажми «Включить Bluetooth-мышь»." };
    private readonly Border pad = new() { Background = new SolidColorBrush(Color.FromRgb(28, 48, 66)), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 16, 0, 12), MinHeight = 190, Focusable = true };
    private Point previous;
    private bool inside, sending, pending, closing;
    private int dx, dy, wheel;
    private byte buttons;
    public MouseWindow()
    {
        Title = "Поток · мышь iPhone"; Width = 650; Height = 660; MinHeight = 620; MinWidth = 580;
        Background = new SolidColorBrush(Color.FromRgb(11, 18, 32));
        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(new TextBlock { Text = "Управление iPhone", FontSize = 25, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = "На iPhone включи: Настройки → Универсальный доступ → Касание → AssistiveTouch. Затем «Устройства → Устройства Bluetooth» и выбери имя этого ПК. Режим разработчика не нужен.", Margin = new Thickness(0, 14, 0, 16) });
        var start = new Button { Content = "Включить Bluetooth-мышь", IsEnabled = false };
        var supported = false;
        pad.IsEnabled = false; pad.Opacity = 0.45;
        async Task CheckSupport()
        {
            supported = false; start.IsEnabled = false;
            try
            {
                var adapter = await Windows.Devices.Bluetooth.BluetoothAdapter.GetDefaultAsync();
                if (adapter == null) { status.Text = "Bluetooth-адаптер не найден."; return; }
                var radio = await adapter.GetRadioAsync();
                if (radio.State != Windows.Devices.Radios.RadioState.On) status.Text = "Включи Bluetooth в Windows и нажми «Проверить адаптер».";
                else if (!adapter.IsPeripheralRoleSupported) status.Text = "Управление недоступно: Bluetooth-адаптер ПК не поддерживает режим BLE peripheral. Включение AssistiveTouch это ограничение не устраняет. Нужен совместимый адаптер.";
                else { supported = true; status.Text = $"Адаптер готов. Имя ПК при сопряжении: {Environment.MachineName}."; }
            }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { start.IsEnabled = supported; }
        }
        start.Click += async (_, _) =>
        {
            start.IsEnabled = false;
            try { await mouse.Start(); status.Text = "Мышь объявлена. Свяжи iPhone с этим ПК через AssistiveTouch."; }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { start.IsEnabled = supported; }
        };
        content.Children.Add(start);
        var check = new Button { Content = "Проверить адаптер", Margin = new Thickness(0, 8, 0, 0) };
        check.Click += async (_, _) => await CheckSupport();
        content.Children.Add(check);
        status.Margin = new Thickness(0, 14, 0, 0); content.Children.Add(status);
        pad.Child = new TextBlock { Text = "ТАЧПАД\nДвижение · левый/правый клик · прокрутка\nEsc — отпустить мышь", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
        content.Children.Add(pad);
        content.Children.Add(new TextBlock { Text = "Управление активно только в этой области и после Bluetooth-подключения. Движения относительные; жестов мультитач пока нет.", FontSize = 12 });
        Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Loaded += async (_, _) => await CheckSupport();
        pad.MouseEnter += (_, e) => { previous = e.GetPosition(pad); inside = true; };
        pad.MouseLeave += (_, _) => { if (!pad.IsMouseCaptured) inside = false; };
        pad.MouseMove += (_, e) =>
        {
            var current = e.GetPosition(pad);
            if (inside && mouse.Connected) { dx += (int)Math.Round(current.X - previous.X); dy += (int)Math.Round(current.Y - previous.Y); }
            previous = current;
        };
        pad.MouseDown += async (_, e) =>
        {
            if (!mouse.Connected) return;
            pad.Focus(); pad.CaptureMouse(); buttons = e.ChangedButton == MouseButton.Right ? (byte)(buttons | 2) : (byte)(buttons | 1);
            await Send();
        };
        pad.MouseUp += async (_, e) =>
        {
            buttons = e.ChangedButton == MouseButton.Right ? (byte)(buttons & ~2) : (byte)(buttons & ~1);
            await Send(); if (buttons == 0) pad.ReleaseMouseCapture();
        };
        pad.MouseWheel += (_, e) => { wheel += e.Delta / 120; e.Handled = true; };
        pad.LostMouseCapture += async (_, _) => { buttons = 0; await Send(); };
        KeyDown += async (_, e) => { if (e.Key == Key.Escape) { buttons = 0; await Send(); pad.ReleaseMouseCapture(); } };
        Deactivated += async (_, _) => { buttons = 0; await Send(); pad.ReleaseMouseCapture(); };
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += async (_, _) =>
        {
            var wasEnabled = pad.IsEnabled;
            pad.IsEnabled = mouse.Connected; pad.Opacity = mouse.Connected ? 1 : 0.45;
            if (wasEnabled && !mouse.Connected) { buttons = 0; dx = dy = wheel = 0; status.Text = "Bluetooth-соединение с iPhone прервано."; }
            if (mouse.Connected) { status.Text = "iPhone подписан на мышь · управление активно"; if (dx != 0 || dy != 0 || wheel != 0 || pending) await Send(); }
        };
        timer.Start();
        Closing += async (_, e) =>
        {
            if (closing) return;
            e.Cancel = true; closing = true; timer.Stop(); buttons = 0; dx = dy = wheel = 0;
            while (sending) await Task.Delay(10);
            await Send(); mouse.Dispose(); Close();
        };
    }
    private async Task Send()
    {
        pending = true;
        if (sending) return;
        sending = true;
        pending = false;
        try
        {
            var x = Math.Clamp(dx, -127, 127); var y = Math.Clamp(dy, -127, 127); var scroll = Math.Clamp(wheel, -127, 127);
            dx -= x; dy -= y; wheel -= scroll;
            await mouse.Send(buttons, x, y, scroll);
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { sending = false; }
    }
}
