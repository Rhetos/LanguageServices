/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
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
            if (line >= lineStarts.Value.Count) return Text.Length;

            var pos = lineStarts.Value[line] + chr;

            if (line < lineStarts.Value.Count - 1 && lineStarts.Value[line] + chr >= lineStarts.Value[line + 1])
                pos = lineStarts.Value[line + 1] - 1;

            if (pos >= Text.Length) return Text.Length;

            if (pos > 0 && Text[pos] == '\n' && Text[pos - 1] == '\r') pos--;
            return pos;
        }

        public LineChr GetLineChr(int pos)
        {
            if (Text.Length == 0) return LineChr.Zero;
            if (pos >= Text.Length) pos = Text.Length - 1;

            if (Text[pos] == '\n' && pos > 0 && Text[pos - 1] == '\r') pos--;

            var line = 0;
            while (line < lineStarts.Value.Count && lineStarts.Value[line] <= pos) line++;
            return new LineChr(line - 1, pos - lineStarts.Value[line - 1]);
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
            line = line
                .Replace('\t', ' ')
                .Replace("\r\n", "")
                .Replace("\n", "");

            if (pos > line.Length) pos = line.Length;
            var posIndicator = "^".PadLeft(pos + 1, ' ');
            return $"{line}\n{posIndicator}";
        }

        public string ShowPosition(LineChr lineChr)
        {
            var pos = GetPosition(lineChr);
            var lineText = ExtractLine(pos);
            return ShowPositionOnLine(lineText, lineChr.Chr);
        }

        public string GetTruncatedAtNextEndOfLine(LineChr lineChr)
        {
            if (Text.Length == 0) return "";
            var pos = GetPosition(lineChr);
            while (pos < Text.Length)
            {
                if (Text[pos++] == '\n') break;
            }

            return Text.Substring(0, pos);
        }

        private List<int> GetLineStartPositions()
        {
            var result = new List<int>() { 0 };
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
