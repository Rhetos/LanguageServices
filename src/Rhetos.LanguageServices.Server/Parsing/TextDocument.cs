using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class TextDocument : IDslScriptsProvider
    {
        public string Text { get; }

        public IEnumerable<DslScript> DslScripts => new[] {new DslScript() { Script = Text }};

        private readonly Lazy<List<int>> lineStarts;

        public TextDocument(string text)
        {
            this.Text = text;
            lineStarts = new Lazy<List<int>>(GetLineStartPositions);
        }

        public int GetPosition(LineChr lineChr)
        {
            return GetPosition(lineChr.Line, lineChr.Chr);
        }

        public int GetPosition(int line, int chr)
        {
            if (Text.Length == 0) return 0;
            if (line >= lineStarts.Value.Count) line = lineStarts.Value.Count - 1;
            var pos = lineStarts.Value[line] + chr;
            
            if (pos >= Text.Length) 
                pos = Text.Length - 1;

            if (line < lineStarts.Value.Count - 1 && lineStarts.Value[line] + chr >= lineStarts.Value[line + 1])
                pos = lineStarts.Value[line + 1] - 1;

            if (Text[pos] == '\n' && pos > 0 && Text[pos - 1] == '\r') pos--;

            return pos;
        }

        public LineChr GetLineChr(int pos)
        {
            if (Text.Length == 0) return LineChr.Zero;
            if (pos >= Text.Length) pos = Text.Length - 1;

            if (Text[pos] == '\n' && pos > 0 && Text[pos - 1] == '\r') pos--;

            var lineStarts = GetLineStartPositions();
            var line = 0;
            while (line < lineStarts.Count && lineStarts[line] <= pos) line++;
            return new LineChr(line - 1, pos - lineStarts[line - 1]);
        }

        public string ExtractLine(int posOnLine)
        {
            var line = GetLineChr(posOnLine).Line;

            var start = lineStarts.Value[line];
            var end = line >= lineStarts.Value.Count - 1
                ? Text.Length
                : lineStarts.Value[line + 1];

            return Text.Substring(start, end - start);
        }

        public static string ShowPositionOnLine(string line, int pos)
        {
            line = line.Replace('\t', ' ');
            if (pos >= line.Length) pos = line.Length - 1;
            var posIndicator = "^".PadLeft(pos + 1, ' ');
            if (!line.EndsWith("\n")) line += "\n";
            return line + posIndicator;
        }

        public string ShowPosition(LineChr lineChr)
        {
            var pos = GetPosition(lineChr);
            var lineText = ExtractLine(pos);
            return ShowPositionOnLine(lineText, lineChr.Chr);
        }

        private List<int> GetLineStartPositions()
        {
            var result = new List<int>();
            result.Add(0);
            for (var i = 0; i < Text.Length; i++)
            {
                if (Text[i] == '\n')
                {
                    result.Add(i + 1);
                }

                if (Text[i] == '\r' && i < Text.Length - 1 && Text[i + 1] == '\n')
                {
                    i++;
                    result.Add(i + 1);
                }
            }

            return result;
        }
    }
}
