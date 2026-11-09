using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
namespace PhoneMirror;
public sealed record StreamMetrics(int? EncoderFps = null, int? SentFps = null, string? Size = null)
{
    public static StreamMetrics Parse(string xml, StreamMetrics previous)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 65536 });
        var elements = XDocument.Load(reader).Root?.Element("dict")?.Elements().ToArray() ?? [];
        int? Value(string name)
        {
            for (var i = 0; i + 1 < elements.Length; i++)
                if (elements[i].Name.LocalName == "key" && elements[i].Value == name && int.TryParse(elements[i + 1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n is >= 0 and <= 1000) return n;
            return null;
        }
        return previous with { EncoderFps = Value("encoderCurrentFPS"), SentFps = Value("sentFramesAvg") };
    }
    public static string? FindSize(string line)
    {
        var match = Regex.Match(line, @"width=\(int\)(\d+).*?height=\(int\)(\d+)");
        if (match.Success) return $"{match.Groups[1].Value} × {match.Groups[2].Value}";
        match = Regex.Match(line, @"(?:video |resolution changed.*?)(\d{2,4})[xX](\d{2,4})");
        return match.Success ? $"{match.Groups[1].Value} × {match.Groups[2].Value}" : null;
    }
    public string Summary => (EncoderFps.HasValue ? $"Отчёт iPhone: кодирование {EncoderFps} кадр/с" : "Ожидаю отчёт iPhone о частоте кадров") + (SentFps.HasValue ? $" · отправка {SentFps} кадр/с" : "") + (Size == null ? "" : $"\nРазмер потока: {Size}");
}
