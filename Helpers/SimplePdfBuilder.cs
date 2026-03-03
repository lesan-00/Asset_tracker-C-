using System.Text;

namespace AssetTracker.Helpers;

public static class SimplePdfBuilder
{
    public static byte[] Build(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var lines = new List<string> { title, string.Join(" | ", headers) };
        lines.AddRange(rows.Select(r => string.Join(" | ", r)));
        lines = lines.Take(65).ToList();

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 10 Tf");
        contentBuilder.AppendLine("13 TL");
        contentBuilder.AppendLine("40 790 Td");
        foreach (var line in lines)
        {
            contentBuilder.Append('(');
            contentBuilder.Append(Escape(line));
            contentBuilder.AppendLine(") Tj");
            contentBuilder.AppendLine("T*");
        }

        contentBuilder.AppendLine("ET");
        var content = contentBuilder.ToString();

        var objects = new List<string>
        {
            "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj",
            "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj",
            "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj",
            "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj",
            $"5 0 obj << /Length {Encoding.ASCII.GetByteCount(content)} >> stream\n{content}endstream endobj"
        };

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true);
        writer.Write("%PDF-1.4\n");
        writer.Flush();

        var offsets = new List<long> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(stream.Position);
            writer.Write(obj);
            writer.Write('\n');
            writer.Flush();
        }

        var xrefStart = stream.Position;
        writer.Write($"xref\n0 {offsets.Count}\n");
        writer.Write("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            writer.Write($"{offsets[i]:D10} 00000 n \n");
        }

        writer.Write($"trailer << /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}
