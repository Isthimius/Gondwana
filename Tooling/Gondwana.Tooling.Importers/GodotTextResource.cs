using System.Text;
using System.Text.RegularExpressions;

namespace Gondwana.Tooling.Importers;

/// <summary>One parsed Godot text-resource section with values retained as raw text.</summary>
/// <param name="Kind">Section header name, such as resource, ext_resource, or sub_resource.</param>
/// <param name="Attributes">Case-sensitive header attribute names and unparsed values, including quotes.</param>
/// <param name="Properties">Mutable, case-sensitive property assignments in the section.</param>
public sealed record GodotResourceSection(string Kind, IReadOnlyDictionary<string, string> Attributes, Dictionary<string, string> Properties);

/// <summary>Parses the section and assignment structure needed by the Godot TileSet importer.</summary>
public static class GodotTextResource
{
    /// <summary>Parses sections and multiline assignments while respecting quoted strings, delimiters, and semicolon comments.</summary>
    /// <param name="text">Complete text-resource contents to parse.</param>
    /// <returns>Sections in source order, with values retained as raw text for format-specific interpretation.</returns>
    /// <exception cref="InvalidDataException">Assignments or delimiters are malformed or unterminated.</exception>
    /// <exception cref="ArgumentException">An attribute or property name is duplicated within its section.</exception>
    public static IReadOnlyList<GodotResourceSection> Parse(string text)
    {
        var sections = new List<GodotResourceSection>();
        var statement = new StringBuilder();
        int depth = 0;
        bool quoted = false, escaped = false, comment = false;
        void Flush()
        {
            string line = statement.ToString().Trim(); statement.Clear();
            if (line.Length == 0) return;
            if (line[0] == '[')
            {
                if (line[^1] != ']') throw new InvalidDataException("Unterminated Godot section.");
                string header = line[1..^1];
                string kind = header.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
                var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Match m in Regex.Matches(header[kind.Length..], "(\\w+)\\s*=\\s*(\"(?:\\\\.|[^\"])*\"|[^\\s]+)"))
                    attributes.Add(m.Groups[1].Value, m.Groups[2].Value);
                sections.Add(new(kind, attributes, new(StringComparer.Ordinal)));
            }
            else
            {
                int equals = line.IndexOf('=');
                if (sections.Count == 0 || equals <= 0) throw new InvalidDataException("Invalid Godot property assignment.");
                sections[^1].Properties.Add(line[..equals].Trim(), line[(equals + 1)..].Trim());
            }
        }
        foreach (char c in text)
        {
            if (comment) { if (c != '\n') continue; comment = false; }
            if (!quoted && c == ';') { comment = true; continue; }
            if (!quoted && c == '\n' && depth == 0) { Flush(); continue; }
            statement.Append(c);
            if (quoted)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') quoted = false;
            }
            else if (c == '"') quoted = true;
            else if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') { if (--depth < 0) throw new InvalidDataException("Unbalanced Godot value."); }
        }
        if (quoted || depth != 0) throw new InvalidDataException("Unterminated Godot value.");
        Flush();
        return sections;
    }

    internal static string String(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"') throw new InvalidDataException("Expected a Godot string.");
        return System.Text.Json.JsonSerializer.Deserialize<string>(value)!;
    }
}
