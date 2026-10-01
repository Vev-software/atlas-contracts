using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// RFC 8785 (JCS) JSON Canonicalization Scheme, the canonical form the landscape share digest
/// detached signature is computed over. Object keys are sorted by UTF-16 code units, there is no
/// whitespace, and all characters above U+2027 are escaped. Whole numbers canonicalize in integer
/// form and other numbers in shortest round-trip representation. The digest fixtures are ASCII,
/// but the implementation follows the full rule so any conforming payload canonicalizes
/// identically across implementations.
/// </summary>
internal static class Jcs
{
    public static string Canonicalize(JsonNode node)
    {
        var builder = new StringBuilder();
        Write(node, builder);
        return builder.ToString();
    }

    public static byte[] CanonicalizeUtf8(JsonNode node) =>
        Encoding.UTF8.GetBytes(Canonicalize(node));

    private static void Write(JsonNode? node, StringBuilder sb)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                break;
            case JsonObject obj:
                WriteObject(obj, sb);
                break;
            case JsonArray arr:
                WriteArray(arr, sb);
                break;
            default:
                WriteElement(ToElement(node!), sb);
                break;
        }
    }

    private static JsonElement ToElement(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            node.WriteTo(writer);
        }

        stream.Position = 0;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    private static void WriteObject(JsonObject obj, StringBuilder sb)
    {
        if (obj.Count == 0)
        {
            sb.Append("{}");
            return;
        }

        sb.Append('{');
        var keys = new List<string>();
        foreach (var pair in obj)
        {
            keys.Add(pair.Key);
        }

        keys.Sort(StringComparer.Ordinal);
        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            WriteString(keys[i], sb);
            sb.Append(':');
            Write(obj[keys[i]], sb);
        }

        sb.Append('}');
    }

    private static void WriteArray(JsonArray arr, StringBuilder sb)
    {
        if (arr.Count == 0)
        {
            sb.Append("[]");
            return;
        }

        sb.Append('[');
        for (var i = 0; i < arr.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            Write(arr[i], sb);
        }

        sb.Append(']');
    }

    private static void WriteElement(JsonElement element, StringBuilder sb)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                sb.Append("null");
                break;
            case JsonValueKind.True:
                sb.Append("true");
                break;
            case JsonValueKind.False:
                sb.Append("false");
                break;
            case JsonValueKind.String:
                WriteString(element.GetString()!, sb);
                break;
            case JsonValueKind.Number:
                WriteNumber(element, sb);
                break;
            default:
                throw new InvalidOperationException("Unsupported JSON element kind.");
        }
    }

    private static void WriteNumber(JsonElement element, StringBuilder sb)
    {
        // Whole numbers canonicalize in integer form (a 5.0 is the number 5), per RFC 8785
        // section 3.2.3. Other numbers use the shortest round-trip representation.
        if (element.TryGetInt64(out long l))
        {
            sb.Append(l.ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (element.TryGetUInt64(out ulong ul))
        {
            sb.Append(ul.ToString(CultureInfo.InvariantCulture));
            return;
        }

        WriteDouble(element.GetDouble(), sb);
    }

    private static void WriteDouble(double d, StringBuilder sb)
    {
        if (double.IsNaN(d) || double.IsInfinity(d))
        {
            throw new InvalidOperationException("JCS does not support NaN or Infinity.");
        }

        if (d == Math.Truncate(d) && Math.Abs(d) < 1e15)
        {
            sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
            return;
        }

        var text = d.ToString("R", CultureInfo.InvariantCulture);
        if (!text.Contains('.') && !text.Contains('e') && !text.Contains('E'))
        {
            text += ".0";
        }

        sb.Append(text);
    }

    private static void WriteString(string s, StringBuilder sb)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < 0x20 || c > 0x2027)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
    }
}
