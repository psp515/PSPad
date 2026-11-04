using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PSPad.Module.Investments.Import;

public static class XlsxReader
{
    public const long MaxBytes = 5 * 1024 * 1024;
    internal const long MaxPartBytes = 16 * 1024 * 1024;
    internal const long MaxTotalBytes = 32 * 1024 * 1024;
    const int MaxSheetRows = 20_100;
    const int MaxColumns = 64;

    static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static XlsxBook Read(Stream xlsx)
    {
        if (xlsx.CanSeek && xlsx.Length > MaxBytes)
            throw TooBig();
        var bytes = ReadBounded(xlsx);
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            return ReadBook(zip);
        }
        catch (Exception e) when (e is InvalidDataException or XmlException or IOException or FormatException or NotSupportedException or OverflowException or ArgumentException or InvalidOperationException)
        {
            throw new XtbFileError("This file is not an Excel workbook.");
        }
    }

    static byte[] ReadBounded(Stream xlsx)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = xlsx.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
                throw TooBig();
        }
        return buffer.ToArray();
    }

    static XtbFileError TooBig() => new("This file is larger than 5 MB.");

    static XlsxBook ReadBook(ZipArchive zip)
    {
        var budget = new long[] { MaxTotalBytes };
        var workbook = Load(zip, budget, "xl/workbook.xml") ?? throw new InvalidDataException("No workbook.");
        var targets = (Load(zip, budget, "xl/_rels/workbook.xml.rels")?.Descendants(PackageRel + "Relationship") ?? [])
            .Where(r => r.Attribute("Id") is not null && r.Attribute("Target") is not null)
            .ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!);
        var shared = ReadSharedStrings(Load(zip, budget, "xl/sharedStrings.xml"));

        var sheets = new List<XlsxSheet>();
        foreach (var sheet in workbook.Descendants(Main + "sheet"))
        {
            var id = (string?)sheet.Attribute(Rel + "id");
            if (id is null || !targets.TryGetValue(id, out var target))
                continue;
            var path = target.StartsWith('/') ? target[1..] : "xl/" + target;
            var document = Load(zip, budget, path) ?? throw new InvalidDataException("Missing sheet part.");
            sheets.Add(new XlsxSheet((string?)sheet.Attribute("name") ?? "", ReadRows(document, shared)));
        }
        return new XlsxBook(sheets);
    }

    static XDocument? Load(ZipArchive zip, long[] budget, string path)
    {
        var entry = zip.GetEntry(path);
        if (entry is null)
            return null;
        var limit = Math.Min(MaxPartBytes, budget[0]);
        using var part = new MemoryStream();
        using (var source = entry.Open())
        {
            var chunk = new byte[81_920];
            int read;
            while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
            {
                part.Write(chunk, 0, read);
                if (part.Length > limit)
                    throw new InvalidDataException("Part too large.");
            }
        }
        budget[0] -= part.Length;
        part.Position = 0;
        using var reader = XmlReader.Create(part, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    static List<string> ReadSharedStrings(XDocument? document) =>
        document is null ? [] : document.Descendants(Main + "si").Select(Concatenate).ToList();

    static string Concatenate(XElement element)
    {
        var text = new StringBuilder();
        foreach (var t in element.Descendants(Main + "t").Where(t => t.Parent?.Name != Main + "rPh"))
            text.Append(t.Value);
        return text.ToString();
    }

    static List<IReadOnlyList<XlsxCell?>> ReadRows(XDocument document, List<string> shared)
    {
        var rows = new List<IReadOnlyList<XlsxCell?>>();
        foreach (var row in document.Descendants(Main + "row"))
        {
            var index = int.TryParse((string?)row.Attribute("r"), out var r) && r > 0 ? r - 1 : rows.Count;
            if (index >= MaxSheetRows)
                throw new XtbFileError("This file has more than 20,000 rows. Export a shorter period.");
            while (rows.Count <= index)
                rows.Add([]);
            rows[index] = ReadCells(row, shared);
        }
        return rows;
    }

    static List<XlsxCell?> ReadCells(XElement row, List<string> shared)
    {
        var cells = new List<XlsxCell?>();
        foreach (var c in row.Elements(Main + "c"))
        {
            var reference = (string?)c.Attribute("r") ?? "";
            var column = reference.Length > 0 ? ColumnIndex(reference) : cells.Count;
            if (column < 0)
                column = cells.Count;
            if (column >= MaxColumns)
                continue;
            while (cells.Count <= column)
                cells.Add(null);
            cells[column] = ReadCell(c, reference, shared);
        }
        return cells;
    }

    static XlsxCell ReadCell(XElement c, string reference, List<string> shared)
    {
        var value = (string?)c.Element(Main + "v");
        switch ((string?)c.Attribute("t"))
        {
            case "s":
                var ok = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < shared.Count;
                return new XlsxCell(reference, ok ? shared[i] : null, null);
            case "inlineStr":
                var inline = c.Element(Main + "is");
                return new XlsxCell(reference, inline is null ? null : Concatenate(inline), null);
            case "str":
            case "d":
                return new XlsxCell(reference, value, null);
            case "e":
                return new XlsxCell(reference, null, null);
            default:
                var number = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : (double?)null;
                return new XlsxCell(reference, null, number);
        }
    }

    static int ColumnIndex(string reference)
    {
        var column = 0;
        var letters = 0;
        foreach (var ch in reference)
        {
            if (ch is >= 'A' and <= 'Z')
                column = column * 26 + (ch - 'A' + 1);
            else if (ch is >= 'a' and <= 'z')
                column = column * 26 + (ch - 'a' + 1);
            else
                break;
            letters++;
        }
        return letters == 0 || column > 16_384 ? -1 : column - 1;
    }
}
