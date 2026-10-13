using System.Text;

namespace PSPad.Module.Money.Import;

public static class CsvReader
{
    const string NoDelimiter = "The header row must separate columns with , or ;.";

    public static CsvTable Read(string text)
    {
        var content = text.TrimStart('﻿');
        var delimiter = DelimiterOf(content);

        if (delimiter is null)
        {
            return CsvTable.Failed(NoDelimiter);
        }

        var (records, error) = Records(content, delimiter.Value);

        if (error is not null)
        {
            return CsvTable.Failed(error);
        }

        var filled = records.Where(record => record.Fields.Any(field => field.Trim().Length > 0)).ToArray();
        return filled.Length == 0
            ? CsvTable.Failed(NoDelimiter)
            : new CsvTable(filled[0].Fields, filled[1..], null);
    }

    static char? DelimiterOf(string content)
    {
        var header = content.Split('\n').Select(line => line.TrimEnd('\r')).FirstOrDefault(line => line.Trim().Length > 0);

        if (header is null)
        {
            return null;
        }

        var (commas, semicolons, quoted) = (0, 0, false);
        foreach (var character in header)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && character == ',')
            {
                commas++;
            }
            else if (!quoted && character == ';')
            {
                semicolons++;
            }
        }

        return (commas > 0, semicolons > 0) switch
        {
            (true, false) => ',',
            (false, true) => ';',
            _ => null
        };
    }

    static (List<CsvLine> Records, string? Error) Records(string content, char delimiter)
    {
        var records = new List<CsvLine>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var (line, start, quoted, fieldStarted) = (1, 1, false, false);

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (quoted)
            {
                if (character == '"' && index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else if (character != '\r')
                {
                    line += character == '\n' ? 1 : 0;
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"' when !fieldStarted:
                    quoted = true;
                    fieldStarted = true;
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    records.Add(new CsvLine(start, fields.ToArray()));
                    (fields, field, fieldStarted) = ([], new StringBuilder(), false);
                    line++;
                    start = line;
                    break;
                case var _ when character == delimiter:
                    fields.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    break;
                default:
                    field.Append(character);
                    fieldStarted = true;
                    break;
            }
        }

        if (quoted)
        {
            return (records, $"Line {start}: a quoted field is never closed.");
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(new CsvLine(start, fields.ToArray()));
        }

        return (records, null);
    }
}
