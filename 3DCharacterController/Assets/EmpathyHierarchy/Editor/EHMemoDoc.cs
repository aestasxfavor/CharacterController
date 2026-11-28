using System.Collections.Generic;
using System.Text;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Text model of one memo column (plain C#, no Unity), made of blocks:
    /// Text / Quote / Callout (free lines with bullet and numbered lists, Tab to indent),
    /// Checklist (every line "☐ item" / "☑ item"), Title, Subtitle, Code and Divider. Saved as markdown.
    /// </summary>
    public class MemoDoc
    {
        public enum Kind { Text, Title, Subtitle, Quote, Code, Divider, Checklist, Callout, Image }

        public class Block
        {
            public Kind kind;
            public string text = "";
            /// <summary>Pictures only: 0 left, 1 center, 2 right (written as ![center](file.png)).</summary>
            public int align;
            public Block(Kind k, string t = "") { kind = k; text = t ?? ""; }
        }

        public const char BoxGlyph = '☐', DoneGlyph = '☑';
        public const string Indent = "    ";
        /// <summary>Every list item starts two spaces in, so the mark sits a little away from the edge.</summary>
        public const string Lead = "  ";
        public const int MaxLevel = 3;
        static readonly char[] BulletGlyphs = { '•', '◦', '▪', '•' };

        public readonly List<Block> Blocks = new List<Block>();

        public MemoDoc() { Blocks.Add(new Block(Kind.Text)); }

        public static bool SingleLine(Kind k) => k == Kind.Title || k == Kind.Subtitle;
        /// <summary>Blocks without text to edit (divider, picture): the caret skips them, Backspace below removes them.</summary>
        public static bool IsObject(Kind k) => k == Kind.Divider || k == Kind.Image;
        /// <summary>Blocks where lists, indenting and numbering work.</summary>
        public static bool TextLike(Kind k) => k == Kind.Text || k == Kind.Quote || k == Kind.Callout;

        public bool IsEmpty
        {
            get
            {
                foreach (var b in Blocks)
                    if (b.kind != Kind.Text || b.text.Trim().Length > 0) return false;
                return true;
            }
        }

        // ================================================================== lines

        static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;

        public static int LineStartAt(string t, int pos)
        {
            pos = Clamp(pos, 0, t.Length);
            return pos == 0 ? 0 : t.LastIndexOf('\n', pos - 1) + 1;
        }

        public static int LineEndAt(string t, int pos)
        {
            int n = t.IndexOf('\n', Clamp(pos, 0, t.Length));
            return n < 0 ? t.Length : n;
        }

        public static bool OnFirstLine(string t, int pos) => LineStartAt(t, pos) == 0;
        public static bool OnLastLine(string t, int pos) => LineEndAt(t, pos) == t.Length;

        static int LineIndex(string t, int pos)
        {
            int n = 0;
            for (int i = 0; i < pos && i < t.Length; i++) if (t[i] == '\n') n++;
            return n;
        }

        static int StartOfLine(string t, int line)
        {
            int s = 0;
            for (int l = 0; l < line; l++)
            {
                int n = t.IndexOf('\n', s);
                if (n < 0) return t.Length;
                s = n + 1;
            }
            return s;
        }

        // ================================================================== checklist boxes

        /// <summary>'☐' or '☑' if the line at <paramref name="pos"/> starts with a box, else '\0'.</summary>
        public static char BoxAt(string t, int pos)
        {
            int s = LineStartAt(t, pos);
            if (s + 1 < t.Length && (t[s] == BoxGlyph || t[s] == DoneGlyph) && t[s + 1] == ' ') return t[s];
            return '\0';
        }

        static string StripBox(string line) =>
            line.Length >= 2 && line[1] == ' ' && (line[0] == BoxGlyph || line[0] == DoneGlyph) ? line.Substring(2) : line;

        public static string ToggleDoneAt(string t, int pos)
        {
            char m = BoxAt(t, pos);
            if (m == '\0') return t;
            var sb = new StringBuilder(t);
            sb[LineStartAt(t, pos)] = m == BoxGlyph ? DoneGlyph : BoxGlyph;
            return sb.ToString();
        }

        /// <summary>Removes every ticked checkbox line.</summary>
        public static string ClearDone(string t)
        {
            var keep = new List<string>();
            foreach (var l in t.Split('\n'))
                if (!(l.Length >= 2 && l[0] == DoneGlyph && l[1] == ' ')) keep.Add(l);
            if (keep.Count == 0) keep.Add("");
            return string.Join("\n", keep);
        }

        // ================================================================== bullet / numbered lists

        public struct ListInfo
        {
            public bool isList, numbered;
            public int level;     // 0..MaxLevel (4 spaces each)
            public int prefixLen; // indent + mark + spacing
        }

        public static char BulletFor(int level) => BulletGlyphs[Clamp(level, 0, BulletGlyphs.Length - 1)];

        /// <summary>1, 2, 3 → a, b, c → i, ii, iii → 1, 2, 3 …</summary>
        public static string NumberLabel(int n, int level)
        {
            switch (level % 3)
            {
                case 1:
                {
                    var sb = new StringBuilder();
                    while (n > 0) { n--; sb.Insert(0, (char)('a' + n % 26)); n /= 26; }
                    return sb.ToString();
                }
                case 2: return Roman(n);
                default: return n.ToString();
            }
        }

        static string Roman(int n)
        {
            string[] r = { "m", "cm", "d", "cd", "c", "xc", "l", "xl", "x", "ix", "v", "iv", "i" };
            int[] v = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            var sb = new StringBuilder();
            for (int i = 0; i < v.Length && n > 0; i++) while (n >= v[i]) { sb.Append(r[i]); n -= v[i]; }
            return sb.ToString();
        }

        static bool LabelFits(string label, int level)
        {
            if (label.Length == 0 || label.Length > 6) return false;
            bool digits = true, letters = true, roman = true;
            foreach (char c in label)
            {
                if (c < '0' || c > '9') digits = false;
                if (c < 'a' || c > 'z') letters = false;
                if ("ivxlcdm".IndexOf(c) < 0) roman = false;
            }
            // numbers work at any level; letters / roman only where they belong (so "a. " in a sentence stays text)
            return digits || (letters && level % 3 == 1) || (roman && level % 3 == 2);
        }

        public static ListInfo ParseList(string line)
        {
            var info = new ListInfo();
            if (!line.StartsWith(Lead)) return info;
            int level = 0, i = Lead.Length;
            while (level < MaxLevel + 1 && string.CompareOrdinal(line, i, Indent, 0, Indent.Length) == 0 && i + Indent.Length <= line.Length)
            {
                level++;
                i += Indent.Length;
            }
            if (i < line.Length && System.Array.IndexOf(BulletGlyphs, line[i]) >= 0 &&
                i + 2 < line.Length + 1 && string.CompareOrdinal(line, i + 1, "  ", 0, 2) == 0)
            {
                info.isList = true;
                info.level = level;
                info.prefixLen = i + 3;
                return info;
            }
            int dot = line.IndexOf(". ", i, System.StringComparison.Ordinal);
            if (dot > i && LabelFits(line.Substring(i, dot - i), level))
            {
                info.isList = true;
                info.numbered = true;
                info.level = level;
                info.prefixLen = dot + 2;
            }
            return info;
        }

        public static string BulletPrefix(int level) => Repeat(level) + BulletFor(level) + "  ";
        public static string NumberPrefix(int n, int level) => Repeat(level) + NumberLabel(n, level) + ". ";

        static string Repeat(int level)
        {
            var sb = new StringBuilder(Lead);
            for (int i = 0; i < level; i++) sb.Append(Indent);
            return sb.ToString();
        }

        static string Rewrap(string line, ListInfo info, bool numbered, int level)
        {
            string body = info.isList ? line.Substring(info.prefixLen) : line;
            return (numbered ? NumberPrefix(1, level) : BulletPrefix(level)) + body;
        }

        /// <summary>Gives every numbered item its number (1. 2. 3., a. b., i. ii.) from its place in the list.</summary>
        public static string Renumber(string t)
        {
            var lines = t.Split('\n');
            var counter = new int[MaxLevel + 2];
            for (int li = 0; li < lines.Length; li++)
            {
                var info = ParseList(lines[li]);
                if (!info.isList)
                {
                    for (int k = 0; k < counter.Length; k++) counter[k] = 0;
                    continue;
                }
                for (int k = info.level + 1; k < counter.Length; k++) counter[k] = 0;
                if (!info.numbered) { counter[info.level] = 0; continue; }
                counter[info.level]++;
                lines[li] = NumberPrefix(counter[info.level], info.level) + lines[li].Substring(info.prefixLen);
            }
            return string.Join("\n", lines);
        }

        /// <summary>Same line and same place in the words after list marks / boxes changed length.</summary>
        public static int Remap(string oldText, string newText, int pos)
        {
            pos = Clamp(pos, 0, oldText.Length);
            int line = LineIndex(oldText, pos);
            int os = LineStartAt(oldText, pos);
            string oldLine = oldText.Substring(os, LineEndAt(oldText, os) - os);
            int col = pos - os;
            int oldPrefix = PrefixLen(oldLine);
            col = System.Math.Max(0, col - oldPrefix);
            int ns = StartOfLine(newText, line);
            string newLine = newText.Substring(ns, LineEndAt(newText, ns) - ns);
            return Clamp(ns + PrefixLen(newLine) + col, ns, ns + newLine.Length);
        }

        static int PrefixLen(string line)
        {
            if (StripBox(line).Length != line.Length) return 2;
            var info = ParseList(line);
            return info.isList ? info.prefixLen : 0;
        }

        static string StripList(string line)
        {
            var info = ParseList(line);
            return info.isList ? line.Substring(info.prefixLen) : line;
        }

        /// <summary>"-" / "*" / "1." typed at the start of a line, then Space: makes a list item. Returns false if not a shortcut.</summary>
        public static bool ListShortcut(string t, int cur, out string nt, out int ncur)
        {
            nt = t; ncur = cur;
            int ls = LineStartAt(t, cur);
            string before = t.Substring(ls, cur - ls);
            int level = 0;
            while (before.StartsWith(Indent) && level < MaxLevel) { before = before.Substring(Indent.Length); level++; }
            before = before.TrimStart(' ');
            bool bullet = before == "-" || before == "*";
            bool number = before.Length >= 2 && before[before.Length - 1] == '.' && LabelFits(before.Substring(0, before.Length - 1), 0);
            if (!bullet && !number) return false;
            string prefix = bullet ? BulletPrefix(level) : NumberPrefix(1, level);
            nt = t.Remove(ls, cur - ls).Insert(ls, prefix);
            nt = Renumber(nt);
            int line = LineIndex(t, cur);
            int ns = StartOfLine(nt, line);
            ncur = ns + PrefixLen(nt.Substring(ns, LineEndAt(nt, ns) - ns));
            return true;
        }

        /// <summary>
        /// Enter on a list item: the next item with the same mark (numbers count on); Enter on an empty item
        /// steps it out one level, or ends the list at the top level.
        /// </summary>
        public static bool ListEnter(string t, int cur, out string nt, out int ncur)
        {
            nt = t; ncur = cur;
            int ls = LineStartAt(t, cur), le = LineEndAt(t, cur);
            string line = t.Substring(ls, le - ls);
            var info = ParseList(line);
            if (!info.isList) return false;
            int line0 = LineIndex(t, cur);
            if (line.Length == info.prefixLen)
            {
                // empty item
                string repl = info.level > 0 ? Rewrap(line, info, info.numbered, info.level - 1) : "";
                nt = Renumber(t.Remove(ls, line.Length).Insert(ls, repl));
                int s = StartOfLine(nt, line0);
                ncur = LineEndAt(nt, s);
                return true;
            }
            int at = System.Math.Max(cur, ls + info.prefixLen);
            string prefix = info.numbered ? NumberPrefix(1, info.level) : BulletPrefix(info.level);
            nt = Renumber(t.Insert(at, "\n" + prefix));
            int ns = StartOfLine(nt, line0 + 1);
            ncur = ns + PrefixLen(nt.Substring(ns, LineEndAt(nt, ns) - ns));
            return true;
        }

        /// <summary>Tab / Shift+Tab on list items (every list line in the selection). False if none is a list item.</summary>
        public static bool ListIndent(string t, int a, int b, int delta, out string nt, out int na, out int nb)
        {
            nt = t; na = a; nb = b;
            if (a > b) { int x = a; a = b; b = x; }
            int first = LineIndex(t, a), last = LineIndex(t, b);
            var lines = t.Split('\n');
            bool any = false;
            for (int li = first; li <= last && li < lines.Length; li++)
            {
                var info = ParseList(lines[li]);
                if (!info.isList) continue;
                int lv = Clamp(info.level + delta, 0, MaxLevel);
                if (lv == info.level) { any = true; continue; }
                lines[li] = Rewrap(lines[li], info, info.numbered, lv);
                any = true;
            }
            if (!any) return false;
            nt = Renumber(string.Join("\n", lines));
            na = Remap(t, nt, a);
            nb = Remap(t, nt, b);
            return true;
        }

        /// <summary>Backspace right after a list mark: one level out, or no mark at the top level.</summary>
        public static bool ListBackspace(string t, int cur, out string nt, out int ncur)
        {
            nt = t; ncur = cur;
            int ls = LineStartAt(t, cur), le = LineEndAt(t, cur);
            string line = t.Substring(ls, le - ls);
            var info = ParseList(line);
            if (!info.isList || cur != ls + info.prefixLen) return false;
            string repl = info.level > 0 ? Rewrap(line, info, info.numbered, info.level - 1) : line.Substring(info.prefixLen);
            nt = Renumber(t.Remove(ls, line.Length).Insert(ls, repl));
            int s = StartOfLine(nt, LineIndex(t, cur));
            ncur = s + PrefixLen(nt.Substring(s, LineEndAt(nt, s) - s));
            return true;
        }

        // ================================================================== blocks
        // Each operation updates the caret: (block index, position in that block's text).

        /// <summary>Merges neighbouring Text (and Checklist) blocks and makes sure there is at least one block.</summary>
        public void Normalize(ref int block, ref int pos)
        {
            for (int i = Blocks.Count - 1; i > 0; i--)
            {
                var k = Blocks[i].kind;
                if ((k != Kind.Text && k != Kind.Checklist) || Blocks[i - 1].kind != k) continue;
                int join = Blocks[i - 1].text.Length + 1;
                Blocks[i - 1].text += "\n" + Blocks[i].text;
                Blocks.RemoveAt(i);
                if (block == i) { block = i - 1; pos += join; }
                else if (block > i) block--;
            }
            if (Blocks.Count == 0) { Blocks.Add(new Block(Kind.Text)); block = 0; pos = 0; }
            block = Clamp(block, 0, Blocks.Count - 1);
            pos = Clamp(pos, 0, Blocks[block].text.Length);
        }

        /// <summary>Text for a block of <paramref name="kind"/> made from these lines.</summary>
        static string Shape(string text, Kind kind)
        {
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                bool done = l.Length >= 2 && l[0] == DoneGlyph && l[1] == ' ';
                l = StripBox(l);
                if (kind == Kind.Checklist) l = (done ? DoneGlyph : BoxGlyph) + " " + StripList(l);
                else if (!TextLike(kind)) l = StripList(l);
                lines[i] = l;
            }
            string r = string.Join("\n", lines);
            return TextLike(kind) ? Renumber(r) : r;
        }

        /// <summary>
        /// Turns the caret's line (or the selected lines) into a block of <paramref name="kind"/>.
        /// Title / Subtitle take one line. In a styled block it switches the kind; the same kind again
        /// turns it back into text.
        /// </summary>
        public void MakeBlock(ref int block, ref int pos, Kind kind, int selEnd = -1)
        {
            var b = Blocks[block];
            if (IsObject(b.kind)) return;
            if (selEnd < 0) selEnd = pos;
            int a = System.Math.Min(pos, selEnd), z = System.Math.Max(pos, selEnd);

            if (b.kind != Kind.Text)
            {
                if (b.kind == kind) kind = Kind.Text;
                if (SingleLine(kind) && b.text.IndexOf('\n') >= 0)
                {
                    // several lines into a title: back to text first, then only the caret's line
                    string plain = Shape(b.text, Kind.Text);
                    pos = Remap(b.text, plain, pos);
                    b.text = plain;
                    b.kind = Kind.Text;
                    Normalize(ref block, ref pos);
                    MakeBlock(ref block, ref pos, kind);
                    return;
                }
                string shaped = Shape(b.text, kind);
                pos = Remap(b.text, shaped, pos);
                b.text = shaped;
                b.kind = kind;
                Normalize(ref block, ref pos);
                return;
            }
            if (kind == Kind.Text) return;

            string t = b.text;
            int ls = SingleLine(kind) ? LineStartAt(t, pos) : LineStartAt(t, a);
            int le = SingleLine(kind) ? LineEndAt(t, pos) : LineEndAt(t, z);
            string chunk = t.Substring(ls, le - ls);
            string shapedChunk = Shape(chunk, kind);
            int newInChunk = Remap(chunk, shapedChunk, pos - ls);

            string head = ls > 0 ? t.Substring(0, ls - 1) : null;
            string tail = le < t.Length ? t.Substring(le + 1) : null;
            int at = block;
            Blocks.RemoveAt(block);
            if (head != null) Blocks.Insert(at++, new Block(Kind.Text, Renumber(head)));
            Blocks.Insert(at, new Block(kind, shapedChunk));
            if (tail != null) Blocks.Insert(at + 1, new Block(Kind.Text, Renumber(tail)));
            block = at;
            pos = newInChunk;
            Normalize(ref block, ref pos);
        }

        /// <summary>
        /// Checklist: the caret's item leaves the list as a text line (Enter on an empty item, or Backspace on the
        /// first item). Items above and below stay checklists.
        /// </summary>
        public void ChecklistLineToText(ref int block, ref int pos)
        {
            var b = Blocks[block];
            string t = b.text;
            int ls = LineStartAt(t, pos), le = LineEndAt(t, pos);
            string body = StripBox(t.Substring(ls, le - ls));
            string head = ls > 0 ? t.Substring(0, ls - 1) : null;
            string tail = le < t.Length ? t.Substring(le + 1) : null;
            int at = block;
            Blocks.RemoveAt(block);
            if (head != null) Blocks.Insert(at++, new Block(Kind.Checklist, head));
            Blocks.Insert(at, new Block(Kind.Text, body));
            if (tail != null) Blocks.Insert(at + 1, new Block(Kind.Checklist, tail));
            block = at;
            pos = 0;
            Normalize(ref block, ref pos);
        }

        /// <summary>Enter in a title / subtitle: the text after the caret goes into the text block below.</summary>
        public void SplitAfter(ref int block, ref int pos)
        {
            var b = Blocks[block];
            pos = Clamp(pos, 0, b.text.Length);
            string right = b.text.Substring(pos);
            b.text = b.text.Substring(0, pos);
            if (block + 1 < Blocks.Count && Blocks[block + 1].kind == Kind.Text)
                Blocks[block + 1].text = right + "\n" + Blocks[block + 1].text;
            else
                Blocks.Insert(block + 1, new Block(Kind.Text, right));
            block += 1;
            pos = 0;
            Normalize(ref block, ref pos);
        }

        /// <summary>Leaves a Quote / Code / Callout from its empty last line: a text block below takes the caret.</summary>
        public void ExitBlock(ref int block, ref int pos)
        {
            var b = Blocks[block];
            int ls = LineStartAt(b.text, b.text.Length);
            if (ls > 0) b.text = b.text.Substring(0, ls - 1);
            int nb = block + 1;
            if (nb >= Blocks.Count || Blocks[nb].kind != Kind.Text) Blocks.Insert(nb, new Block(Kind.Text));
            block = nb;
            pos = 0;
            Normalize(ref block, ref pos);
        }

        /// <summary>
        /// Backspace at the very start of a block: a styled block becomes text first; a text block joins the
        /// block above (a divider above is removed instead).
        /// </summary>
        public void BackspaceAtStart(ref int block, ref int pos)
        {
            var b = Blocks[block];
            if (b.kind == Kind.Checklist)
            {
                ChecklistLineToText(ref block, ref pos);
                return;
            }
            if (b.kind != Kind.Text)
            {
                string shaped = Shape(b.text, Kind.Text);
                b.text = shaped;
                b.kind = Kind.Text;
                pos = 0;
                Normalize(ref block, ref pos);
                return;
            }
            if (block == 0) return;
            var prev = Blocks[block - 1];
            if (IsObject(prev.kind))
            {
                Blocks.RemoveAt(block - 1);
                block--;
                pos = 0;
                Normalize(ref block, ref pos);
                return;
            }
            if ((SingleLine(prev.kind) || prev.kind == Kind.Checklist) && b.text.IndexOf('\n') >= 0)
            {
                // several lines can't go into a title / one item: just move to the end of it
                block--;
                pos = prev.text.Length;
                return;
            }
            int join = prev.text.Length;
            prev.text += b.text;
            Blocks.RemoveAt(block);
            block--;
            pos = join;
            Normalize(ref block, ref pos);
        }

        /// <summary>Divider below the caret's line (an empty line is replaced by it); the caret goes below it.</summary>
        public void InsertDivider(ref int block, ref int pos) => InsertObject(ref block, ref pos, new Block(Kind.Divider));

        /// <summary>Picture below the caret's line, like a divider.</summary>
        public void InsertImage(ref int block, ref int pos, string id) => InsertObject(ref block, ref pos, new Block(Kind.Image, id));

        /// <summary>Takes out an object block (divider / picture); the caret goes to the text after it.</summary>
        public void RemoveObject(int index, out int block, out int pos)
        {
            Blocks.RemoveAt(index);
            block = System.Math.Max(0, index - 1);
            pos = Blocks.Count > 0 ? Blocks[block].text.Length : 0;
            Normalize(ref block, ref pos);
        }

        void InsertObject(ref int block, ref int pos, Block obj)
        {
            var b = Blocks[block];
            if (b.kind != Kind.Text)
            {
                Blocks.Insert(block + 1, obj);
                if (block + 2 >= Blocks.Count || Blocks[block + 2].kind != Kind.Text)
                    Blocks.Insert(block + 2, new Block(Kind.Text));
                block += 2;
                pos = 0;
                Normalize(ref block, ref pos);
                return;
            }
            string t = b.text;
            int ls = LineStartAt(t, pos), le = LineEndAt(t, pos);
            bool emptyLine = le == ls;
            string before = emptyLine ? (ls > 0 ? t.Substring(0, ls - 1) : null) : t.Substring(0, le);
            string after = le < t.Length ? t.Substring(le + 1) : "";
            int at = block;
            Blocks.RemoveAt(block);
            if (before != null) Blocks.Insert(at++, new Block(Kind.Text, Renumber(before)));
            Blocks.Insert(at++, obj);
            Blocks.Insert(at, new Block(Kind.Text, Renumber(after)));
            block = at;
            pos = 0;
            Normalize(ref block, ref pos);
        }

        // ================================================================== code colouring

        static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch", "char", "class",
            "const", "continue", "default", "delegate", "do", "double", "else", "enum", "event", "false", "finally",
            "float", "for", "foreach", "get", "if", "in", "int", "interface", "internal", "is", "lock", "long",
            "namespace", "new", "null", "object", "out", "override", "params", "partial", "private", "protected",
            "public", "readonly", "ref", "return", "sealed", "set", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "using", "var", "virtual", "void", "where", "while", "yield",
        };

        /// <summary>Rich text for a code block: keywords, strings, comments and numbers in colour (C#-like).</summary>
        public static string Highlight(string code, string keyword, string str, string comment, string number)
        {
            var sb = new StringBuilder();
            int i = 0, n = code.Length;
            while (i < n)
            {
                char c = code[i];
                if (c == '/' && i + 1 < n && code[i + 1] == '/')
                {
                    int e = code.IndexOf('\n', i);
                    if (e < 0) e = n;
                    Colored(sb, code, i, e, comment);
                    i = e;
                    continue;
                }
                if (c == '/' && i + 1 < n && code[i + 1] == '*')
                {
                    int e = code.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                    e = e < 0 ? n : e + 2;
                    Colored(sb, code, i, e, comment);
                    i = e;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    int e = i + 1;
                    while (e < n && code[e] != c && code[e] != '\n') { if (code[e] == '\\') e++; e++; }
                    if (e < n && code[e] == c) e++;
                    e = System.Math.Min(e, n);
                    Colored(sb, code, i, e, str);
                    i = e;
                    continue;
                }
                if (char.IsDigit(c) && (i == 0 || !IsWord(code[i - 1])))
                {
                    int e = i + 1;
                    while (e < n && (char.IsLetterOrDigit(code[e]) || code[e] == '.' || code[e] == '_')) e++;
                    Colored(sb, code, i, e, number);
                    i = e;
                    continue;
                }
                if (IsWord(c))
                {
                    int e = i + 1;
                    while (e < n && IsWord(code[e])) e++;
                    string w = code.Substring(i, e - i);
                    if (Keywords.Contains(w)) Colored(sb, code, i, e, keyword);
                    else Plain(sb, code, i, e);
                    i = e;
                    continue;
                }
                Plain(sb, code, i, i + 1);
                i++;
            }
            return sb.ToString();
        }

        static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

        static void Colored(StringBuilder sb, string s, int a, int b, string color)
        {
            sb.Append("<color=").Append(color).Append('>');
            Plain(sb, s, a, b);
            sb.Append("</color>");
        }

        static void Plain(StringBuilder sb, string s, int a, int b)
        {
            for (int i = a; i < b; i++)
            {
                char c = s[i];
                if (c == '<') sb.Append("<noparse><</noparse>");
                else if (c == ' ') sb.Append('\u00A0');            // keep indentation: labels collapse spaces
                else if (c == '\t') sb.Append("\u00A0\u00A0\u00A0\u00A0");
                else sb.Append(c);
            }
        }

        /// <summary>The first line with words in it (marks removed): used as a page's name in the page list.</summary>
        public static string FirstLine(string md)
        {
            var doc = FromMarkdown(md);
            foreach (var b in doc.Blocks)
            {
                if (IsObject(b.kind)) continue;
                foreach (var raw in b.text.Split('\n'))
                {
                    string l = StripList(StripBox(raw)).Trim();
                    if (l.Length > 0) return l;
                }
            }
            return "";
        }

        // ================================================================== markdown

        static string LineToMd(string l)
        {
            var info = ParseList(l);
            if (!info.isList) return l;
            string md = l.Substring(Lead.Length); // markdown has no lead
            if (!info.numbered) md = md.Substring(0, info.level * Indent.Length) + "- " + l.Substring(info.prefixLen);
            return md;
        }

        static string LineFromMd(string s)
        {
            int level = 0, i = 0;
            while (level < MaxLevel && string.CompareOrdinal(s, i, Indent, 0, Indent.Length) == 0) { i += Indent.Length; level++; }
            string rest = s.Substring(i);
            if (rest.StartsWith("- ") || rest.StartsWith("* ")) return BulletPrefix(level) + rest.Substring(2);
            var asList = ParseList(Lead + s);
            if (asList.isList && asList.numbered) return Lead + s;
            return s;
        }

        static string Lines(string text, System.Func<string, string> map, string linePrefix = "")
        {
            var ls = text.Split('\n');
            var sb = new StringBuilder();
            for (int j = 0; j < ls.Length; j++)
            {
                if (j > 0) sb.Append('\n');
                sb.Append(linePrefix).Append(map(ls[j]));
            }
            return sb.ToString();
        }

        public static MemoDoc FromMarkdown(string md)
        {
            var doc = new MemoDoc();
            doc.Blocks.Clear();
            var lines = (md ?? "").Replace("\r", "").Split('\n');
            var text = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i];
                if (s.StartsWith("```") || s.StartsWith(":::"))
                {
                    Flush(doc, text);
                    string fence = s.Substring(0, 3);
                    var inner = new List<string>();
                    i++;
                    while (i < lines.Length && !lines[i].StartsWith(fence)) inner.Add(lines[i++]);
                    if (fence == "```") doc.Blocks.Add(new Block(Kind.Code, string.Join("\n", inner)));
                    else doc.Blocks.Add(new Block(Kind.Callout, Renumber(Lines(string.Join("\n", inner), x => LineFromMd(Clean(x))))));
                    continue;
                }
                if (s.StartsWith("## ")) { Flush(doc, text); doc.Blocks.Add(new Block(Kind.Subtitle, Clean(s.Substring(3)))); continue; }
                if (s.StartsWith("# ")) { Flush(doc, text); doc.Blocks.Add(new Block(Kind.Title, Clean(s.Substring(2)))); continue; }
                if (s == "---") { Flush(doc, text); doc.Blocks.Add(new Block(Kind.Divider)); continue; }
                if (s.StartsWith("![") && s.EndsWith(")") && s.IndexOf("](", System.StringComparison.Ordinal) > 0)
                {
                    Flush(doc, text);
                    int close = s.IndexOf("](", System.StringComparison.Ordinal);
                    int open = close + 2;
                    string alt = s.Substring(2, close - 2);
                    doc.Blocks.Add(new Block(Kind.Image, s.Substring(open, s.Length - open - 1))
                    {
                        align = alt == "center" ? 1 : alt == "right" ? 2 : 0
                    });
                    continue;
                }
                if (s.StartsWith(">"))
                {
                    Flush(doc, text);
                    var q = new List<string>();
                    while (i < lines.Length && lines[i].StartsWith(">"))
                    {
                        string l = lines[i].Substring(1);
                        if (l.StartsWith(" ")) l = l.Substring(1);
                        q.Add(LineFromMd(Clean(l)));
                        i++;
                    }
                    i--;
                    doc.Blocks.Add(new Block(Kind.Quote, Renumber(string.Join("\n", q))));
                    continue;
                }
                if (s.StartsWith("- [ ] ") || s.StartsWith("- [x] ") || s.StartsWith("- [X] "))
                {
                    Flush(doc, text);
                    var items = new List<string>();
                    while (i < lines.Length && (lines[i].StartsWith("- [ ] ") || lines[i].StartsWith("- [x] ") || lines[i].StartsWith("- [X] ")))
                    {
                        string l = lines[i];
                        items.Add((l[3] == ' ' ? BoxGlyph : DoneGlyph) + " " + Clean(l.Substring(6)));
                        i++;
                    }
                    i--;
                    doc.Blocks.Add(new Block(Kind.Checklist, string.Join("\n", items)));
                    continue;
                }
                text.Add(LineFromMd(Clean(s)));
            }
            Flush(doc, text);
            int bi = 0, p = 0;
            doc.Normalize(ref bi, ref p);
            return doc;
        }

        static void Flush(MemoDoc doc, List<string> text)
        {
            if (text.Count == 0) return;
            doc.Blocks.Add(new Block(Kind.Text, Renumber(string.Join("\n", text))));
            text.Clear();
        }

        /// <summary>Drops **bold** / __underline__ markers left by an earlier memo version.</summary>
        static string Clean(string s) => s.Replace("**", "").Replace("__", "");

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Blocks.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                var b = Blocks[i];
                switch (b.kind)
                {
                    case Kind.Title: sb.Append("# ").Append(b.text); break;
                    case Kind.Subtitle: sb.Append("## ").Append(b.text); break;
                    case Kind.Divider: sb.Append("---"); break;
                    case Kind.Image: sb.Append("![").Append(b.align == 1 ? "center" : b.align == 2 ? "right" : "").Append("](").Append(b.text).Append(')'); break;
                    case Kind.Code: sb.Append("```\n").Append(b.text).Append("\n```"); break;
                    case Kind.Callout: sb.Append(":::\n").Append(Lines(b.text, LineToMd)).Append("\n:::"); break;
                    case Kind.Quote: sb.Append(Lines(b.text, LineToMd, "> ")); break;
                    case Kind.Checklist:
                        sb.Append(Lines(b.text, l => (l.Length >= 1 && l[0] == DoneGlyph ? "- [x] " : "- [ ] ") + StripBox(l)));
                        break;
                    default: sb.Append(Lines(b.text, LineToMd)); break;
                }
            }
            return sb.ToString();
        }
    }
}
