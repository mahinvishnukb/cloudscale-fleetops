using System.Text;

namespace FleetOps.Application.Manifests;

/// <summary>
/// Minimal RFC 4180 CSV reader. Hand-rolled rather than pulled from a package so the
/// quoting rules — escaped double quotes, embedded commas, embedded newlines, CRLF —
/// are explicit and directly unit-testable.
/// </summary>
public static class Rfc4180Reader
{
    public static IReadOnlyList<IReadOnlyList<string>> ReadAll(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var records = new List<IReadOnlyList<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();

        var inQuotes = false;
        var fieldWasQuoted = false;
        var i = 0;

        void EndField()
        {
            fields.Add(fieldWasQuoted ? field.ToString() : field.ToString().Trim());
            field.Clear();
            fieldWasQuoted = false;
        }

        void EndRecord()
        {
            EndField();

            // Skip records that are entirely empty (trailing newline, blank lines).
            if (fields.Count > 0 && fields.Any(f => !string.IsNullOrWhiteSpace(f)))
            {
                records.Add(fields.ToArray());
            }

            fields.Clear();
        }

        while (i < content.Length)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // A doubled quote inside a quoted field is a literal quote.
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    fieldWasQuoted = true;
                    i++;
                    break;

                case ',':
                    EndField();
                    i++;
                    break;

                case '\r':
                    // Swallow CR; the following LF ends the record.
                    i++;
                    break;

                case '\n':
                    EndRecord();
                    i++;
                    break;

                default:
                    field.Append(c);
                    i++;
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            EndRecord();
        }

        return records;
    }
}
