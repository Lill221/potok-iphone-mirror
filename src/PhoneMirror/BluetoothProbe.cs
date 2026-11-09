using Windows.Devices.Bluetooth;
using System.Text.Json;

namespace PhoneMirror;
public static class BluetoothProbe
{
    public static async Task<string> Run()
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null) return JsonSerializer.Serialize(new { available = false, reason = "Bluetooth-адаптер не найден" });
            var radio = await adapter.GetRadioAsync();
            return JsonSerializer.Serialize(new { available = true, name = radio.Name, state = radio.State.ToString(), peripheral = adapter.IsPeripheralRoleSupported, central = adapter.IsCentralRoleSupported });
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { available = false, reason = ex.Message }); }
    }
}
