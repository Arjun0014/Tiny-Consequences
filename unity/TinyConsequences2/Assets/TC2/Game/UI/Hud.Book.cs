using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.History;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 27: the History Book as a book) The reader as an open book: two leaves of warm paper bound in leather, the gutter's shadow between
    /// them; the contents on the left leaf, the chapter on the right, set in EB Garamond with titles in IM FELL English (OFL; CREDITS). A chapter
    /// opens with its part's name, its title, its years and a rule, its first paragraph under a raised initial; its great events stand apart with
    /// their years in the margin in red, the notable records under them in plain ink, the lesser ones small and faded, the notes that tell an age's
    /// lesser records together in italic, the tally at its foot like a footnote; the leaves are turned at their foot, a folio between. A line
    /// chosen opens its causes back to their root. Reads the book (HistoryBook) only; writes nothing.
    /// </summary>
    public sealed partial class Hud
    {
        Font _serif, _display;
        Texture2D _leaf, _gutter, _leather;
        GUIStyle _bkBook, _bkBookSub, _bkKicker, _bkTitle, _bkSub, _bkBody, _bkHead, _bkGreat, _bkGreatSel, _bkLine, _bkLineSel, _bkMinor, _bkMinorSel, _bkNote, _bkTally,
                 _bkYear, _bkToc, _bkTocSel, _bkTocPart, _bkTocPartSel, _bkNav, _bkFolio, _bkWhy;
        static readonly Color Ink = new Color(0.16f, 0.11f, 0.07f), Faded = new Color(0.42f, 0.34f, 0.25f), Rubric = new Color(0.55f, 0.15f, 0.08f);

        void BookStyles()
        {
            if (_bkBody != null) return;
            _serif = Resources.Load<Font>("Fonts/EBGaramond");
            _display = Resources.Load<Font>("Fonts/IMFellEnglish");
            if (_display == null) _display = _serif;
            _leaf = PaperLeaf(256, 320);
            _gutter = Gutter(64);
            _leather = Solid(new Color(0.27f, 0.14f, 0.08f, 1f));
            var hover = Solid(new Color(0.55f, 0.30f, 0.12f, 0.09f));
            var chosen = Solid(new Color(0.55f, 0.30f, 0.12f, 0.17f));
            GUIStyle S(Font f, int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft, int l = 0, int rt = 0, int t = 0, int b = 0)
            {
                var st = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, fontStyle = fs, alignment = a, wordWrap = true, richText = true, padding = new RectOffset(l, rt, t, b), margin = new RectOffset(0, 0, 0, 0) };
                st.normal.textColor = c;
                return st;
            }
            _bkBook = S(_display, 30, Rubric, b: 2);
            _bkBookSub = S(_serif, 15, Faded, FontStyle.Italic, b: 10);
            _bkKicker = S(_display, 13, Rubric, b: 2);
            _bkTitle = S(_display, 32, Ink, b: 0);
            _bkSub = S(_serif, 16, Faded, FontStyle.Italic, b: 6);
            _bkBody = S(_serif, 18, Ink, t: 4, b: 8);
            _bkHead = S(_display, 21, Rubric, t: 14, b: 4);
            _bkNote = S(_serif, 16, new Color(0.30f, 0.23f, 0.16f), FontStyle.Italic, l: 14, t: 3, b: 5);
            _bkTally = S(_serif, 14, Faded, FontStyle.Italic, t: 6, b: 4);
            _bkYear = S(_display, 16, Rubric, a: TextAnchor.UpperRight, rt: 10, t: 3);
            _bkWhy = S(_serif, 15, Ink, l: 6, t: 1, b: 1);
            _bkWhy.hover.textColor = Rubric; _bkWhy.hover.background = hover;
            GUIStyle Row(int size, Color c, FontStyle fs, GUIStyle[] sel)
            {
                var st = S(_serif, size, c, fs, l: 2, rt: 4, t: 3, b: 4);
                st.hover.textColor = Rubric; st.hover.background = hover;
                var s2 = new GUIStyle(st); s2.normal.textColor = Rubric; s2.normal.background = chosen;
                sel[0] = s2;
                return st;
            }
            var tmp = new GUIStyle[1];
            _bkGreat = Row(19, Ink, FontStyle.Bold, tmp); _bkGreatSel = tmp[0];
            _bkLine = Row(17, Ink, FontStyle.Normal, tmp); _bkLineSel = tmp[0];
            _bkMinor = Row(15, Faded, FontStyle.Normal, tmp); _bkMinorSel = tmp[0];
            _bkToc = S(_serif, 16, Ink, l: 18, t: 1, b: 1); _bkToc.hover.textColor = Rubric; _bkToc.hover.background = hover;
            _bkTocSel = new GUIStyle(_bkToc); _bkTocSel.normal.textColor = Rubric; _bkTocSel.normal.background = chosen;
            _bkTocPart = S(_display, 18, Ink, t: 6, b: 1); _bkTocPart.hover.textColor = Rubric; _bkTocPart.hover.background = hover;
            _bkTocPartSel = new GUIStyle(_bkTocPart); _bkTocPartSel.normal.textColor = Rubric;
            _bkNav = S(_display, 17, Rubric, a: TextAnchor.MiddleCenter, l: 6, rt: 6, t: 2, b: 2); _bkNav.hover.textColor = Ink; _bkNav.hover.background = hover;
            _bkFolio = S(_display, 15, Faded, a: TextAnchor.MiddleCenter);
            // the rest of the book's pages (the preface's paragraphs, the timeline, the realm maps' captions) in the book's type
            if (_serif != null)
            {
                foreach (var st in new[] { _ink, _inkFact, _inkSub, _inkLine, _inkLineSel, _toc, _tocSel }) st.font = _serif;
                foreach (var st in new[] { _inkTitle, _inkHead, _tocPart, _inkBtn }) st.font = _display;
                _ink.fontSize = 16; _inkFact.fontSize = 14; _inkSub.fontSize = 15; _inkHead.fontSize = 19; _inkTitle.fontSize = 28;
            }
        }

        /// <summary>A leaf of paper: warm, a little uneven and fibrous, darker toward its edges (a texture stretched over a leaf).</summary>
        static Texture2D PaperLeaf(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "tc2-leaf" };
            var rng = new System.Random(1027);
            var px = new Color32[w * h];
            var blot = new float[(w / 16 + 2) * (h / 16 + 2)];
            for (int i = 0; i < blot.Length; i++) blot[i] = (float)rng.NextDouble();
            int bw = w / 16 + 2;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x / 16f, fy = y / 16f; int ix = (int)fx, iy = (int)fy; float tx = fx - ix, ty = fy - iy;
                    tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                    float m = Mathf.Lerp(Mathf.Lerp(blot[iy * bw + ix], blot[iy * bw + ix + 1], tx), Mathf.Lerp(blot[(iy + 1) * bw + ix], blot[(iy + 1) * bw + ix + 1], tx), ty);
                    float grain = (float)rng.NextDouble();
                    float u = x / (float)(w - 1), v = y / (float)(h - 1);
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u) * 1.6f, Mathf.Min(v, 1f - v) * 1.3f);
                    float vign = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 9f));
                    float k = (0.955f + 0.04f * m + 0.018f * grain) * Mathf.Lerp(0.84f, 1f, vign);
                    px[y * w + x] = new Color32((byte)(236 * k), (byte)(224 * k), (byte)(198 * k), 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>The gutter's shadow: dark in the fold, fading out either side.</summary>
        static Texture2D Gutter(int w)
        {
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "tc2-gutter" };
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x - (w - 1) * 0.5f) / ((w - 1) * 0.5f);
                tex.SetPixel(x, 0, new Color(0.25f, 0.15f, 0.06f, 0.42f * Mathf.Pow(1f - d, 2.2f)));
            }
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>The spread: the binding, two leaves, the gutter; the contents on the left leaf and the chapter (or the preface, the timeline) on the right.</summary>
        void BookSpread(Rect r, TC2.Core.Simulation.World w)
        {
            BookStyles();
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(r.x - 9f, r.y - 7f, r.width + 18f, r.height + 14f), _leather);
                GUI.color = new Color(0f, 0f, 0f, 0.35f); GUI.DrawTexture(new Rect(r.x - 9f, r.yMax + 7f, r.width + 18f, 5f), _dot); GUI.color = Color.white;
            }
            float lw = Mathf.Round(r.width * 0.36f);
            Rect left = new Rect(r.x, r.y, lw, r.height), right = new Rect(r.x + lw, r.y, r.width - lw, r.height);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(left, _leaf);
                GUI.DrawTexture(right, _leaf);
                GUI.DrawTexture(new Rect(r.x + lw - 34f, r.y, 68f, r.height), _gutter);
            }
            // the left leaf: the book's title and its contents
            GUILayout.BeginArea(new Rect(left.x + 30f, left.y + 26f, left.width - 60f, left.height - 46f));
            GUILayout.Label(_writer.Book.Title, _bkBook);
            GUILayout.Label(_writer.Book.Sub, _bkBookSub);
            Rule(0.0f, 0.65f);
            _listScroll = GUILayout.BeginScrollView(_listScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            if (GUILayout.Button("Preface", _bookPart == -1 ? _bkTocPartSel : _bkTocPart)) { _bookPart = -1; _bookScroll = Vector2.zero; _line = 0; }
            if (GUILayout.Button("A Timeline of the World", _bookPart == TimelinePart ? _bkTocPartSel : _bkTocPart)) { _bookPart = TimelinePart; _bookScroll = Vector2.zero; _line = 0; }
            for (int i = 0; i < HistoryBook.PartTitles.Length; i++)
            {
                string label = "<color=#8c2614>" + HistoryBook.Roman(i + 1) + ".</color>  " + HistoryBook.PartTitles[i];
                if (GUILayout.Button(label, _bookPart == i ? _bkTocPartSel : _bkTocPart) && _bookPart != i) { Part(i); _bookPart = i; _bookChapter = 0; _bookScroll = Vector2.zero; _line = 0; }
                if (_bookPart != i || _parts[i] == null) continue;
                var chs = _parts[i].Chapters;
                for (int k = 0; k < chs.Count; k++)
                {
                    // (session 27) the ages with their years, faded beside the name: the eras at a glance (and "the Second Years of Plenty" placed)
                    string toc = chs[k].Title;
                    if (i == 0 && !string.IsNullOrEmpty(chs[k].Sub) && chs[k].Sub.StartsWith("years ", System.StringComparison.Ordinal))
                        toc += "  <size=12><color=#8a7458>" + chs[k].Sub.Substring(6).Replace(" to ", "–") + "</color></size>";
                    if (GUILayout.Button(toc, k == _bookChapter ? _bkTocSel : _bkToc) && k != _bookChapter) { _bookChapter = k; _bookScroll = Vector2.zero; _line = 0; }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            // the right leaf
            GUILayout.BeginArea(new Rect(right.x + 40f, right.y + 26f, right.width - 72f, right.height - 40f));
            BookChapter ch = null;
            if (_bookPart == TimelinePart) { Kicker("A Timeline"); TimelinePage(w, r); }
            else if (_bookPart < 0)
            {
                Kicker("The Preface");
                GUILayout.Label("Preface", _bkTitle);
                GUILayout.Label("the world in year " + _writer.Book.Year, _bkSub);
                Rule(0.0f, 1f);
                _bookScroll = GUILayout.BeginScrollView(_bookScroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(Mathf.Max(120f, right.height - 200f)));
                bool first = true;
                foreach (var para in _writer.Book.Preface) { GUILayout.Label(first ? Initial(para) : para, _bkBody); first = false; }
                GUILayout.EndScrollView();
            }
            else
            {
                var part = _parts[_bookPart];
                if (_bookChapter >= part.Chapters.Count) _bookChapter = part.Chapters.Count - 1;
                Kicker("Part " + HistoryBook.Roman(_bookPart + 1) + "  ·  " + HistoryBook.PartTitles[_bookPart]);
                if (_bookChapter < 0) { GUILayout.Label(HistoryBook.PartTitles[_bookPart], _bkTitle); GUILayout.Label(part.Intro ?? "", _bkBody); }
                else
                {
                    ch = part.Chapters[_bookChapter];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(ch.Title, _bkTitle);
                    if (ch.Settlement > 0 && w.Settlements.Exists(ch.Settlement))
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("go there", _bkNav, GUILayout.Width(90))) { var a = w.Settlements.Get(ch.Settlement).Anchor; Rig.Lat = a.Latitude; Rig.Lon = a.Longitude; }
                    }
                    GUILayout.EndHorizontal();
                    if (!string.IsNullOrEmpty(ch.Sub)) GUILayout.Label(ch.Sub, _bkSub);
                    Rule(0.0f, 1f);
                    _bookScroll = GUILayout.BeginScrollView(_bookScroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(Mathf.Max(120f, right.height - 200f) * (_line != 0 ? 0.62f : 1f)));   // (room left for the leaf's foot: it was clipped at 720 px)
                    if (_bookChapter == 0 && !string.IsNullOrEmpty(part.Intro)) GUILayout.Label(part.Intro, _bkNote);
                    if (ch.MapSeat > 0 && w.Settlements.Exists(ch.MapSeat)) RealmMap(w, ch, right.width);
                    bool lead = true;
                    foreach (var b in ch.Blocks)
                    {
                        switch (b.Kind)
                        {
                            case BookKind.Heading: GUILayout.Label(b.Text, _bkHead); break;
                            case BookKind.Fact: Rule(0.0f, 0.3f); GUILayout.Label(b.Text, _bkTally); break;
                            case BookKind.Note: GUILayout.Label("–  " + b.Text, _bkNote); break;
                            case BookKind.Line: Entry(in b); break;
                            default: GUILayout.Label(lead ? Initial(b.Text) : b.Text, _bkBody); lead = false; break;
                        }
                    }
                    GUILayout.EndScrollView();
                }
            }
            // a chosen line's causes, back to their root
            if (_line != 0 && w.History.TryGet(_line, out var chosen))
            {
                Rule(0.0f, 1f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Why it happened", _bkHead);
                GUILayout.FlexibleSpace();
                if (Place(w, chosen, out double lat, out double lon) && GUILayout.Button("go there", _bkNav, GUILayout.Width(90))) { Rig.Lat = lat; Rig.Lon = lon; }
                GUILayout.EndHorizontal();
                GUILayout.Label("each cause is a record of its own; the chain's root is " + (chosen.RootKind == RootKind.You ? "your hand" : chosen.RootKind == RootKind.Nature ? "nature" : "people's choices"), _bkTally);
                _whyScroll = GUILayout.BeginScrollView(_whyScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
                int n = w.History.CausalChain(_line, _chain);
                for (int i = 0; i < n; i++)
                {
                    if (!w.History.TryGet(_chain[i], out var e)) { GUILayout.Label("(a record forgotten)", _bkTally); break; }
                    string row = (i == 0 ? "" : "<color=#8c2614>←</color>  ") + HistoryText.Dated(w, e);
                    if (GUILayout.Button(row, _chain[i] == _line ? _bkLineSel : _bkWhy) && _chain[i] != _line) { _line = _chain[i]; break; }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.FlexibleSpace();
            // the foot of the leaf: back, the folio, on
            GUILayout.BeginHorizontal();
            if (_bookPart >= 0 || _bookPart == TimelinePart) { if (GUILayout.Button("‹  previous", _bkNav, GUILayout.Width(120))) Turn(-1); }
            else GUILayout.Space(120);
            GUILayout.FlexibleSpace();
            GUILayout.Label("—  " + Folio() + "  —", _bkFolio);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("next  ›", _bkNav, GUILayout.Width(120))) Turn(1);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void Kicker(string text) => GUILayout.Label(text.ToUpperInvariant(), _bkKicker);

        /// <summary>A thin rule of the page's red ink, from a share of the leaf's width to another.</summary>
        void Rule(float from, float to)
        {
            Rect line = GUILayoutUtility.GetRect(10f, 9f, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = new Color(Rubric.r, Rubric.g, Rubric.b, 0.55f);
            GUI.DrawTexture(new Rect(line.x + line.width * from, line.y + 4f, line.width * (to - from), 1f), _dot);
            GUI.color = Color.white;
        }

        /// <summary>A paragraph's first letter raised and in red (a chapter's opening).</summary>
        string Initial(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int i = 0;
            while (i < text.Length && !char.IsLetter(text[i])) i++;
            if (i >= text.Length) return text;
            return text.Substring(0, i) + "<size=" + Mathf.RoundToInt(_bkBody.fontSize * 2.4f) + "><color=#8c2614>" + text[i] + "</color></size>" + text.Substring(i + 1);
        }

        /// <summary>A record in the chapter: its year in the margin in red, its words by its weight - a great event in bold, a notable record in plain ink,
        /// a lesser one small and faded; chosen, its causes open beneath.</summary>
        void Entry(in BookBlock b)
        {
            string text = b.Text, year = "", season = "";
            int colon = text.IndexOf(": ", System.StringComparison.Ordinal);
            if (text.StartsWith("Year ", System.StringComparison.Ordinal) && colon > 0)
            {
                string date = text.Substring(5, colon - 5);
                int comma = date.IndexOf(',');
                year = comma > 0 ? date.Substring(0, comma) : date;
                season = comma > 0 ? date.Substring(comma + 1).Trim() : "";
                text = text.Substring(colon + 2);
            }
            bool sel = _line == b.Event;
            var st = b.Weight >= 2 ? (sel ? _bkGreatSel : _bkGreat) : b.Weight == 1 ? (sel ? _bkLineSel : _bkLine) : (sel ? _bkMinorSel : _bkMinor);
            GUILayout.BeginHorizontal();
            int ys = b.Weight >= 2 ? 18 : b.Weight == 1 ? 16 : 14;
            GUILayout.Label("<size=" + ys + ">" + year + "</size>" + (season.Length > 0 && b.Weight > 0 ? "\n<size=12><i>" + season + "</i></size>" : ""), _bkYear, GUILayout.Width(86));
            if (GUILayout.Button(text, st)) { _line = sel ? 0 : b.Event; _whyScroll = Vector2.zero; }
            GUILayout.EndHorizontal();
        }

        /// <summary>The leaf's number: the chapters before it in the book, counted from the preface.</summary>
        string Folio()
        {
            if (_bookPart == -1) return "i";
            if (_bookPart == TimelinePart) return "ii";
            int n = 1;
            for (int i = 0; i < _bookPart && i < _parts.Length; i++) n += _parts[i] != null ? _parts[i].Chapters.Count : 1;
            return (n + System.Math.Max(0, _bookChapter)).ToString(CultureInfo.InvariantCulture);
        }
    }
}
