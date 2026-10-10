using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// JCS canonicalization for the v2 conformance vectors (safe integer counts and sequences).
/// Strings preserve Unicode and object keys sort by UTF-16. Not a general-purpose number serializer.
/// </summary>
internal static class DigestV2Jcs
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
        // The conformance vectors use safe integer counts and sequences only.
        // Reject unsupported numbers instead of claiming a full ECMAScript number serializer.
        var number = element.GetDouble();
        if (!double.IsFinite(number) || number != Math.Truncate(number) || Math.Abs(number) > 9007199254740991d)
            throw new InvalidOperationException("This digest conformance helper requires safe integers.");
        sb.Append(((long)number).ToString(CultureInfo.InvariantCulture));
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
                    if (c < 0x20)
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
