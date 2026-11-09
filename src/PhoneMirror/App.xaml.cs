using System.IO;
using System.Windows;

namespace PhoneMirror;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--native-host-test"))
        {
            try { await NativeHostTest.Run(); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "native-host-test.failed.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--probe-bluetooth"))
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "bluetooth-probe.json"), await BluetoothProbe.Run());
            Shutdown(0); return;
        }
        if (e.Args.Contains("--self-test"))
        {
            try { SelfTest.Run(); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.failed.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        var window = new MainWindow();
        if (e.Args.Contains("--sharp")) window.ProfileBox.SelectedIndex = 3;
        MainWindow = window;
        if (e.Args.Contains("--smoke-test"))
        {
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) => { timer.Stop(); window.Close(); };
            timer.Start();
        }
        else if (e.Args.Contains("--start")) window.Loaded += async (_, _) => await window.StartReceiver();
        window.Show();
    }
}
