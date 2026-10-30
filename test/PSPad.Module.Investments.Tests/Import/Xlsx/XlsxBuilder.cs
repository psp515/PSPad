using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace PSPad.Module.Investments.Tests.Import.Xlsx;

public static class XlsxBuilder
{
    public static MemoryStream Book(params (string Name, object?[][] Rows)[] sheets)
    {
        var shared = new List<string>();
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>");
            var workbook = new StringBuilder("<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            var rels = new StringBuilder("<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (var i = 0; i < sheets.Length; i++)
            {
                var n = i + 1;
                workbook.Append($"<sheet name=\"{SecurityElement.Escape(sheets[i].Name)}\" sheetId=\"{n}\" r:id=\"rId{n}\"/>");
                rels.Append($"<Relationship Id=\"rId{n}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{n}.xml\"/>");
                Add(zip, $"xl/worksheets/sheet{n}.xml", SheetXml(sheets[i].Rows, shared));
            }
            workbook.Append("</sheets></workbook>");
            rels.Append("</Relationships>");
            Add(zip, "xl/workbook.xml", workbook.ToString());
            Add(zip, "xl/_rels/workbook.xml.rels", rels.ToString());
            var strings = new StringBuilder("<?xml version=\"1.0\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            foreach (var s in shared)
                strings.Append($"<si><t>{SecurityElement.Escape(s)}</t></si>");
            strings.Append("</sst>");
            Add(zip, "xl/sharedStrings.xml", strings.ToString());
        }
        stream.Position = 0;
        return stream;
    }

    public static MemoryStream RawSheets(params string[] sheetXmls)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var workbook = new StringBuilder("<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            var rels = new StringBuilder("<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (var i = 0; i < sheetXmls.Length; i++)
            {
                var n = i + 1;
                workbook.Append($"<sheet name=\"S{n}\" sheetId=\"{n}\" r:id=\"rId{n}\"/>");
                rels.Append($"<Relationship Id=\"rId{n}\" Type=\"x\" Target=\"worksheets/sheet{n}.xml\"/>");
                Add(zip, $"xl/worksheets/sheet{n}.xml", sheetXmls[i]);
            }
            Add(zip, "xl/workbook.xml", workbook.Append("</sheets></workbook>").ToString());
            Add(zip, "xl/_rels/workbook.xml.rels", rels.Append("</Relationships>").ToString());
        }
        stream.Position = 0;
        return stream;
    }

    public static string SheetWith(string sheetData, string padding = "") =>
        $"<?xml version=\"1.0\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>{padding}{sheetData}</sheetData></worksheet>";

    public static MemoryStream Bytes(byte[] content) => new(content);

    static string SheetXml(object?[][] rows, List<string> shared)
    {
        var xml = new StringBuilder("<?xml version=\"1.0\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        for (var r = 0; r < rows.Length; r++)
        {
            xml.Append($"<row r=\"{r + 1}\">");
            for (var c = 0; c < rows[r].Length; c++)
            {
                var reference = $"{(char)('A' + c)}{r + 1}";
                switch (rows[r][c])
                {
                    case null:
                        break;
                    case string s:
                        shared.Add(s);
                        xml.Append($"<c r=\"{reference}\" t=\"s\"><v>{shared.Count - 1}</v></c>");
                        break;
                    case InlineText t:
                        xml.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(t.Value)}</t></is></c>");
                        break;
                    case FormulaText f:
                        xml.Append($"<c r=\"{reference}\" t=\"str\"><f>A1</f><v>{SecurityElement.Escape(f.Value)}</v></c>");
                        break;
                    case var number:
                        xml.Append($"<c r=\"{reference}\"><v>{Convert.ToDouble(number, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                        break;
                }
            }
            xml.Append("</row>");
        }
        xml.Append("</sheetData></worksheet>");
        return xml.ToString();
    }

    static void Add(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
