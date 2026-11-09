using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace PhoneMirror;

// HID over GATT, relative mouse. Phone pairing is performed by the user in AssistiveTouch.
public sealed class BleMouse : IDisposable
{
    private GattServiceProvider? provider;
    private GattLocalCharacteristic? report;
    public bool Connected => report?.SubscribedClients.Count == 1;
    public bool Advertising => provider?.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started;
    private static Guid Uuid(int value) => new($"0000{value:x4}-0000-1000-8000-00805f9b34fb");
    public static readonly byte[] ReportMap = [
        0x05,0x01,0x09,0x02,0xA1,0x01,0x85,0x01,0x09,0x01,0xA1,0x00,
        0x05,0x09,0x19,0x01,0x29,0x03,0x15,0x00,0x25,0x01,0x95,0x03,0x75,0x01,0x81,0x02,
        0x95,0x01,0x75,0x05,0x81,0x03,0x05,0x01,0x09,0x30,0x09,0x31,0x09,0x38,
        0x15,0x81,0x25,0x7F,0x75,0x08,0x95,0x03,0x81,0x06,0xC0,0xC0];

    public static byte[] Pack(byte buttons, int x, int y, int wheel) =>
        [(byte)(buttons & 7), unchecked((byte)(sbyte)Math.Clamp(x, -127, 127)),
         unchecked((byte)(sbyte)Math.Clamp(y, -127, 127)), unchecked((byte)(sbyte)Math.Clamp(wheel, -127, 127))];

    public async Task Start()
    {
        Dispose();
        var adapter = await BluetoothAdapter.GetDefaultAsync();
        if (adapter == null) throw new InvalidOperationException("Bluetooth-адаптер не найден.");
        var radio = await adapter.GetRadioAsync();
        if (radio.State != Windows.Devices.Radios.RadioState.On) throw new InvalidOperationException("Включи Bluetooth в Windows и повтори проверку.");
        if (!adapter.IsPeripheralRoleSupported) throw new InvalidOperationException("Этот Bluetooth-адаптер не поддерживает BLE peripheral. Для управления с ПК нужен совместимый адаптер; трансляция работает без него.");
        var result = await GattServiceProvider.CreateAsync(Uuid(0x1812));
        if (result.Error != BluetoothError.Success) throw new InvalidOperationException($"Windows не разрешила HID-сервис: {result.Error}.");
        provider = result.ServiceProvider;
        try
        {
            await Static(0x2A4A, [0x11, 0x01, 0x00, 0x02]); // HID 1.11, normally connectable
            await Static(0x2A4B, ReportMap);
            var control = await Characteristic(0x2A4C, GattCharacteristicProperties.WriteWithoutResponse);
            control.WriteRequested += async (sender, args) =>
            {
                var deferral = args.GetDeferral();
                try { var request = await args.GetRequestAsync(); if (request?.Option == GattWriteOption.WriteWithResponse) request.Respond(); }
                finally { deferral.Complete(); }
            };
            report = await Characteristic(0x2A4D, GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify, Pack(0, 0, 0, 0));
            var descriptor = await report.CreateDescriptorAsync(Uuid(0x2908), new GattLocalDescriptorParameters
            { StaticValue = new byte[] { 1, 1 }.AsBuffer(), ReadProtectionLevel = GattProtectionLevel.Plain });
            if (descriptor.Error != BluetoothError.Success) throw new InvalidOperationException($"HID descriptor: {descriptor.Error}");
            provider.StartAdvertising(new GattServiceProviderAdvertisingParameters { IsConnectable = true, IsDiscoverable = true });
            await Task.Delay(700);
            if (!Advertising) throw new InvalidOperationException($"Не удалось объявить Bluetooth-мышь: {provider.AdvertisementStatus}");
        }
        catch { Dispose(); throw; }
    }

    private async Task Static(int uuid, byte[] value) => await Characteristic(uuid, GattCharacteristicProperties.Read, value);
    private async Task<GattLocalCharacteristic> Characteristic(int uuid, GattCharacteristicProperties properties, byte[]? value = null)
    {
        var parameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = properties,
            ReadProtectionLevel = GattProtectionLevel.Plain,
            WriteProtectionLevel = GattProtectionLevel.EncryptionRequired
        };
        if (value != null) parameters.StaticValue = value.AsBuffer();
        var result = await provider!.Service.CreateCharacteristicAsync(Uuid(uuid), parameters);
        if (result.Error != BluetoothError.Success) throw new InvalidOperationException($"HID characteristic {uuid:x4}: {result.Error}");
        return result.Characteristic;
    }
    public async Task Send(byte buttons, int x, int y, int wheel = 0)
    {
        if (report == null || !Connected) return;
        var results = await report.NotifyValueAsync(Pack(buttons, x, y, wheel).AsBuffer());
        if (results.Any(r => r.Status != GattCommunicationStatus.Success)) throw new InvalidOperationException("Bluetooth-соединение с iPhone прервано.");
    }
    public void Dispose() { provider?.StopAdvertising(); provider = null; report = null; }
}
