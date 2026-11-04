using System.Globalization;
using System.Text;
using TC2.Core.Foundation;
using TC2.Core.History;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 32: what came of it) A cast's consequences as a tree on a leaf of the book: the cast at the top left in its power's colour, then what came of
    /// it - at once, the days after, that year, years on, decades on - column by column, each card what followed from the card it hangs from
    /// (Core/History/Consequences), elbowed lines between them, the thread back to the cast lit under the pointer. Over the columns, every record the cast has
    /// led to counted by when it came, and "the latest" goes to the card that came last. A card opens what it is about - a life, a place's page, a work, the
    /// record and its why in the book. The tree is read from the ledger on opening and once a second while it grows (it follows the cast for as long as the
    /// world records what came of it); nothing here writes the world. Opened from the seal at the top right (the last cast), the impact card, Your hand and
    /// the book.
    /// </summary>
    public sealed partial class Hud
    {
        bool _treeOn, _treeFromBook, _treeFresh;
        long _treeCast, _treeLedger = -1;
        float _treeAt = -99f, _treeW, _treeH, _treeDrag;
        ConsequenceTree _tree;
        Vector2 _treeScroll;
        Rect[] _treeCards = new Rect[0];
        string[] _treeKick = new string[0], _treeTip = new string[0], _treeGoWords = new string[0];
        string[] _treeFitK = new string[0], _treeFitT = new string[0];   // (session 32 QA) each card's kicker and words fitted to its space, at their first drawing
        int[] _treeAct = new int[0], _treeTab = new int[0], _treePath = new int[0];
        long[] _treeTo = new long[0];
        readonly string[] _treeColHead = new string[Consequences.MaxDepth + 1], _treeColSpan = new string[Consequences.MaxDepth + 1];
        readonly int[] _treeColMask = new int[Consequences.MaxDepth + 1];
        int _treePower = -1, _treeHover = -1;
        string _treeSub = "", _treeWhen = "";
        int _treeLatestCard = -1;
        float _treeLatestLit = -99f;
        GUIStyle _tcKick, _tcTitle, _tcKickCast, _tcTitleCast, _tcHead, _tcSpan, _tcBadge, _tcBeyond, _tcWhen;
        TC2.Core.Simulation.World _treeLatestW;
        long _treeLatest, _treeLatestThrough;

        const float TcCardW = 252f, TcCardH = 92f, TcGapX = 60f, TcGapY = 12f, TcPad = 14f;
        const int ActNone = 0, ActPerson = 1, ActPlace = 2, ActBuilding = 3, ActRecord = 4, ActGo = 5, ActChapter = 6;
        static readonly Color TierNow = new Color(0.70f, 0.20f, 0.10f), TierDays = new Color(0.80f, 0.56f, 0.12f), TierYear = new Color(0.36f, 0.52f, 0.16f),
                              TierYears = new Color(0.20f, 0.35f, 0.62f), TierDecades = new Color(0.44f, 0.20f, 0.56f);
        static readonly Color[] FamilyInk =
        {
            new Color(0.48f, 0.40f, 0.30f),   // other
            new Color(0.52f, 0.10f, 0.08f),   // death
            new Color(0.88f, 0.42f, 0.10f),   // fire
            new Color(0.50f, 0.33f, 0.18f),   // the earth
            new Color(0.25f, 0.45f, 0.70f),   // the sky
            new Color(0.80f, 0.60f, 0.14f),   // food
            new Color(0.42f, 0.56f, 0.14f),   // sickness
            new Color(0.47f, 0.22f, 0.52f),   // people
            new Color(0.17f, 0.25f, 0.50f),   // power
            new Color(0.12f, 0.50f, 0.52f),   // works
            new Color(0.86f, 0.62f, 0.22f),   // the cast
        };

        /// <summary>(session 32) Opens "What came of it" for a cast (its GodCommand record's id): the tree of what followed from it.</summary>
        public void OpenConsequences(long castId) => OpenConsequences(castId, false);

        /// <summary>(session 32, final) The newest cast the ledger keeps (0 none), for the seal at the top: read on from where it last looked, so a frame costs
        /// nothing (a cast is a Historical record: the memory pass never drops one).</summary>
        long LatestCastSeen(TC2.Core.Simulation.World w)
        {
            if (w == null) return 0;
            var h = w.History;
            if (!ReferenceEquals(w, _treeLatestW)) { _treeLatestW = w; _treeLatest = Consequences.LatestCast(w); _treeLatestThrough = h.LastId; return _treeLatest; }
            if (h.LastId == _treeLatestThrough) return _treeLatest;
            for (int i = h.FirstIndexAfter(_treeLatestThrough); i < h.Count; i++) if (h.RefAt(i).Kind == HistoryKind.GodCommand) _treeLatest = h.RefAt(i).Id;
            _treeLatestThrough = h.LastId;
            return _treeLatest;
        }

        /// <summary>(session 32, final) The seal at the top right for the tree of the last cast: dim before any cast, lit while the tree is open.</summary>
        void TreeSeal(Rect r, Color cream)
        {
            var w = Host != null ? Host.World : null;
            long last = LatestCastSeen(w);
            if (Seal(r, "why", _treeOn, last == 0, cream, out bool hover))
            {
                if (_treeOn) CloseConsequences();
                else if (last != 0) OpenConsequences(last);
            }
            if (hover) TipAt(r, last == 0 ? "<b>What came of it</b>\nCast a power: the tree of everything it leads to grows here, from that hour to decades on"
                                          : "<b>What came of it</b>\nThe tree of your last cast: everything it has led to - at once, that year, years and decades on", false);
        }

        void OpenConsequences(long castId, bool fromBook)
        {
            var w = Host != null ? Host.World : null;
            if (w == null || !w.History.TryGet(castId, out var e) || e.Kind != HistoryKind.GodCommand) return;
            _treeFromBook = fromBook && _book;
            _treeOn = true; _treeCast = castId; _treeFresh = true; _treeScroll = Vector2.zero; _treeHover = -1;
            _book = false; _aim = -1; _personPage = false; _placeSid = 0; _building = 0;
            _treeAt = Time.unscaledTime; _treeLedger = w.History.LastId;
            _tree = Consequences.Build(w, castId);
            if (_tree != null) TreeLayout(w);
            AudioView.Ui("page");
        }

        /// <summary>Closes the tree (back to the book when it was opened from there).</summary>
        public void CloseConsequences()
        {
            if (!_treeOn) return;
            _treeOn = false; _tree = null;
            if (_treeFromBook) _book = true;
            _treeFromBook = false;
        }

        /// <summary>The tree holds the screen while it is open (the time and the seals above it): true when it drew the frame. The book asked for closes it.</summary>
        bool TreeHolds(TC2.Core.Simulation.World w, float sw, float sh)
        {
            if (!_treeOn) return false;
            if (_book) { _treeOn = false; _treeFromBook = false; _tree = null; return false; }
            if (_menuOn) return false;
            TimeBar(w, sw);
            SealBar(sw);
            float topY = Mathf.Max(72f, _left.yMax + 8f), bw = Mathf.Min(1560f, sw - 36f);
            TreePage(new Rect(sw * 0.5f - bw * 0.5f, topY, bw, sh - 22f - topY), w);
            DrawTip();
            KeepSettings();
            AudioView.Duck = 0.5f;
            PointerOverUi = true;
            WorldObjectsView.Pointed = 0; WorldObjectsView.PointedHome = 0;
            _uiRects.Clear();
            return true;
        }

        void TreeStyles()
        {
            BookStyles();
            if (_tcTitle != null) return;
            GUIStyle S(Font f, int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft, bool wrap = true)
            {
                var st = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, fontStyle = fs, alignment = a, wordWrap = wrap, richText = true, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
                st.normal.textColor = c;
                return st;
            }
            _tcKick = S(_display, 11, Rubric, wrap: false);
            _tcTitle = S(_serif, 15, Ink);
            _tcKickCast = S(_display, 11, new Color(1f, 0.86f, 0.52f), wrap: false);
            _tcTitleCast = S(_serif, 16, new Color(1f, 0.96f, 0.86f), FontStyle.Bold);
            _tcHead = S(_display, 14, Ink, wrap: false);
            _tcSpan = S(_serif, 12, Faded, FontStyle.Italic, wrap: false);
            _tcBadge = S(_display, 12, new Color(1f, 0.95f, 0.85f), a: TextAnchor.MiddleCenter, wrap: false);
            _tcBeyond = S(_serif, 12, Faded, FontStyle.Italic, a: TextAnchor.MiddleLeft, wrap: false);
            _tcWhen = S(_display, 12, Ink, wrap: false);
        }

        /// <summary>The leaf: the cast's words, the columns' tiers, the cards and their lines, panned by a drag, the wheel or the arrows.</summary>
        void TreePage(Rect r, TC2.Core.Simulation.World w)
        {
            TreeStyles();
            // (read on opening, then once a second while the ledger grows - at a frame's layout, so its events all see one tree)
            if (Event.current.type == EventType.Layout && Time.unscaledTime - _treeAt > 1f && w.History.LastId != _treeLedger)
            {
                _treeAt = Time.unscaledTime; _treeLedger = w.History.LastId;
                var nt = Consequences.Build(w, _treeCast);
                if (nt != null && _tree != null && nt.Records == _tree.Records && nt.Capped == _tree.Capped) _tree.CastsAfter = nt.CastsAfter;   // (nothing new came of it: the cards stand as laid)
                else { _tree = nt; if (_tree != null) TreeLayout(w); }
            }
            if (Event.current.type == EventType.Repaint) { Binding(new Rect(r.x - 8f, r.y - 7f, r.width + 16f, r.height + 14f)); GUI.DrawTexture(r, _leaf); }
            bool close = false, book = false;
            GUILayout.BeginArea(new Rect(r.x + 26f, r.y + 14f, r.width - 46f, 108f));
            GUILayout.BeginHorizontal();
            Kicker("What came of it");
            GUILayout.FlexibleSpace();
            bool latest = false;
            if (_tree != null && _treeLatestCard > 0 && GUILayout.Button("the latest  ›", _bkNav, GUILayout.ExpandWidth(false))) latest = true;
            if (_tree != null && GUILayout.Button("every record, in the book  ›", _bkNav, GUILayout.ExpandWidth(false))) book = true;
            if (GUILayout.Button("close  ×", _bkNav, GUILayout.Width(84))) close = true;
            GUILayout.EndHorizontal();
            GUILayout.Label(_tree != null ? _tree.Root.Title : "A cast the world no longer remembers", _pgTitle);
            GUILayout.Label(_tree != null ? _treeSub : "Its record has gone from the ledger, and what came of it with it.", _pgSub);
            if (_tree != null && _treeWhen.Length > 0) GUILayout.Label(_treeWhen, _tcWhen);
            GUILayout.EndArea();
            if (close) { CloseConsequences(); AudioView.Ui("click"); return; }
            if (book) { TreeGo(w, -1); return; }
            if (_tree == null) return;

            var strip = new Rect(r.x + 18f, r.y + 122f, r.width - 36f, 38f);
            var view = new Rect(strip.x, strip.yMax + 2f, strip.width, r.yMax - 30f - strip.yMax - 2f);
            float maxX = Mathf.Max(0f, _treeW - view.width), maxY = Mathf.Max(0f, _treeH - view.height);
            // (session 32, final) laid from the top: the cast at the top left, its threads down and to the right - it had stood in the middle of the leaf's
            // height with the empty paper above and below it (s32pow4)
            const float oy = 0f;
            if (_treeFresh && _tree.Nodes.Count > 0) { _treeFresh = false; _treeScroll = Vector2.zero; }
            if (latest && _treeLatestCard > 0 && _treeLatestCard < _treeCards.Length)
            {
                var lc = _treeCards[_treeLatestCard];
                _treeScroll = new Vector2(Mathf.Clamp(lc.x - 40f, 0f, maxX), Mathf.Clamp(lc.y - 40f, 0f, maxY));
                _treeLatestLit = Time.unscaledTime;
                AudioView.Ui("tick");
            }
            // panning: a drag over the leaf, the wheel (with shift, or where it cannot go down, along), the arrows
            var ev = Event.current;
            bool inView = view.Contains(ev.mousePosition);
            if (ev.type == EventType.MouseDown && ev.button == 0 && inView) { _treeDrag = 0f; ev.Use(); }
            else if (ev.type == EventType.MouseDrag && ev.button == 0 && inView) { _treeDrag += ev.delta.magnitude; _treeScroll -= ev.delta; ev.Use(); }
            else if (ev.type == EventType.ScrollWheel && inView)
            {
                float d = ev.delta.y * 24f;
                if (ev.shift || maxY <= 0f) _treeScroll.x += d; else _treeScroll.y += d;
                _treeScroll.x += ev.delta.x * 24f;
                ev.Use();
            }
            else if (ev.type == EventType.KeyDown && GUIUtility.keyboardControl == 0)
            {
                if (ev.keyCode == KeyCode.RightArrow) { _treeScroll.x += 120f; ev.Use(); }
                else if (ev.keyCode == KeyCode.LeftArrow) { _treeScroll.x -= 120f; ev.Use(); }
                else if (ev.keyCode == KeyCode.DownArrow) { _treeScroll.y += 90f; ev.Use(); }
                else if (ev.keyCode == KeyCode.UpArrow) { _treeScroll.y -= 90f; ev.Use(); }
            }
            _treeScroll.x = Mathf.Clamp(_treeScroll.x, 0f, maxX); _treeScroll.y = Mathf.Clamp(_treeScroll.y, 0f, maxY);
            Vector2 off = new Vector2(-_treeScroll.x, oy - _treeScroll.y);

            // the columns' heads: when they came, in the tiers' colours
            GUI.BeginGroup(strip);
            int cols = _tree.MaxDepth + 1;
            for (int d = 0; d < cols && d < _treeColHead.Length; d++)
            {
                float x = TcPad + d * (TcCardW + TcGapX) + off.x;
                if (x > strip.width || x + TcCardW < 0f) continue;
                if (ev.type == EventType.Repaint)
                {
                    int m = _treeColMask[d], k = 0, tiers = 0;
                    for (int b = 0; b < Consequences.Tiers; b++) if ((m & (1 << b)) != 0) tiers++;
                    for (int b = 0; b < Consequences.Tiers; b++)
                    {
                        if ((m & (1 << b)) == 0) continue;
                        float seg = TcCardW / Mathf.Max(1, tiers);
                        var o = GUI.color; GUI.color = TierColour(b, 0.85f);
                        GUI.DrawTexture(new Rect(x + k * seg, 34f, seg - (k < tiers - 1 ? 2f : 0f), 3f), _dot);
                        GUI.color = o; k++;
                    }
                }
                if (ev.type != EventType.Repaint) continue;
                GUI.Label(new Rect(x, 0f, TcCardW, 18f), _treeColHead[d], _tcHead);
                GUI.Label(new Rect(x, 17f, TcCardW, 16f), _treeColSpan[d], _tcSpan);
            }
            GUI.EndGroup();

            // the tree: lines first, then the cards; only what shows is drawn
            _treeHover = -1;
            GUI.BeginGroup(view);
            Vector2 mp = ev.mousePosition;
            Rect seen = new Rect(-off.x - 40f, -off.y - 40f, view.width + 80f, view.height + 80f);
            int n = _tree.Nodes.Count;
            if (inView) for (int i = 0; i < n; i++) { var c = _treeCards[i]; c.position += off; if (c.Contains(mp)) { _treeHover = i; break; } }
            // the thread from the card under the pointer back to the cast
            for (int i = 0; i < n; i++) _treePath[i] = 0;
            for (int i = _treeHover; i >= 0; i = _tree.Nodes[i].Parent) _treePath[i] = 1;
            if (ev.type == EventType.Repaint)
            {
                for (int i = 0; i < n; i++)
                {
                    var nd = _tree.Nodes[i];
                    if (nd.Children.Count == 0) continue;
                    var pc = _treeCards[i];
                    float lo = pc.center.y, hi = pc.center.y;
                    foreach (int ci in nd.Children) { lo = Mathf.Min(lo, _treeCards[ci].center.y); hi = Mathf.Max(hi, _treeCards[ci].center.y); }
                    if (hi < seen.y || lo > seen.yMax || pc.xMax > seen.xMax || pc.xMax + TcGapX < seen.x) continue;
                    float mx = pc.xMax + TcGapX * 0.5f;
                    var faint = new Color(Faded.r, Faded.g, Faded.b, 0.55f);
                    TreeRule(new Rect(pc.xMax, pc.center.y - 1f, TcGapX * 0.5f, 2f), _treePath[i] == 1 && _treeHover != i ? Rubric : faint, off);
                    TreeRule(new Rect(mx - 1f, lo - 1f, 2f, hi - lo + 2f), faint, off);
                    foreach (int ci in nd.Children)
                    {
                        var cc = _treeCards[ci];
                        bool lit = _treePath[ci] == 1;
                        if (lit) TreeRule(new Rect(mx - 1.5f, Mathf.Min(pc.center.y, cc.center.y) - 1.5f, 3f, Mathf.Abs(cc.center.y - pc.center.y) + 3f), Rubric, off);
                        TreeRule(new Rect(mx, cc.center.y - (lit ? 1.5f : 1f), cc.x - mx - 3f, lit ? 3f : 2f), lit ? Rubric : faint, off);
                        TreeRule(new Rect(cc.x - 5f, cc.center.y - 3f, 5f, 6f), lit ? Rubric : faint, off);   // (the line's end at the card)
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                var c = _treeCards[i];
                if (!c.Overlaps(seen)) continue;
                c.position += off;
                TreeCard(i, c);
            }
            // a click (not a drag) on a card goes to what it is about
            if (ev.type == EventType.MouseUp && ev.button == 0 && inView)
            {
                if (_treeDrag < 6f && _treeHover >= 0) { int go = _treeHover; GUI.EndGroup(); ev.Use(); TreeGo(w, go); return; }
                ev.Use();
            }
            GUI.EndGroup();
            // where the leaf has more: thin bars at its right and its foot
            if (ev.type == EventType.Repaint)
            {
                var o = GUI.color;
                if (maxY > 0f)
                {
                    float th = Mathf.Max(30f, view.height * view.height / _treeH), ty = view.y + (view.height - th) * (_treeScroll.y / maxY);
                    GUI.color = new Color(Faded.r, Faded.g, Faded.b, 0.18f); GUI.DrawTexture(new Rect(view.xMax - 4f, view.y, 4f, view.height), _dot);
                    GUI.color = new Color(Rubric.r, Rubric.g, Rubric.b, 0.55f); GUI.DrawTexture(new Rect(view.xMax - 4f, ty, 4f, th), _dot);
                }
                if (maxX > 0f)
                {
                    float tw = Mathf.Max(30f, view.width * view.width / _treeW), tx = view.x + (view.width - tw) * (_treeScroll.x / maxX);
                    GUI.color = new Color(Faded.r, Faded.g, Faded.b, 0.18f); GUI.DrawTexture(new Rect(view.x, view.yMax - 4f, view.width, 4f), _dot);
                    GUI.color = new Color(Rubric.r, Rubric.g, Rubric.b, 0.55f); GUI.DrawTexture(new Rect(tx, view.yMax - 4f, tw, 4f), _dot);
                }
                GUI.color = o;
            }
            GUI.Label(new Rect(r.x + 26f, r.yMax - 26f, r.width - 52f, 20f), "Each card came of the one it hangs from, as the world recorded it.  Hover a card for who and what; click it to go there.  Drag, the wheel or the arrows to look along it.  Esc closes.", _tcSpan);
            if (_treeHover >= 0 && _treeHover < _treeTip.Length && ev.type == EventType.Repaint)
            {
                var c = _treeCards[_treeHover];
                if (_treeTip[_treeHover] == null) _treeTip[_treeHover] = TreeTip(w, _treeHover);   // (written when first pointed at)
                TipAt(new Rect(view.x + c.x + off.x, view.y + c.y + off.y, c.width, c.height), _treeTip[_treeHover], false);
            }
        }

        static Color TierColour(int tier, float a)
        {
            var c = tier == (int)ConsequenceTier.Immediate ? TierNow : tier == (int)ConsequenceTier.Days ? TierDays : tier == (int)ConsequenceTier.Secondary ? TierYear
                  : tier == (int)ConsequenceTier.LongTerm ? TierYears : tier == (int)ConsequenceTier.Decades ? TierDecades : FamilyInk[Consequences.FamCast];
            return new Color(c.r, c.g, c.b, a);
        }

        void TreeRule(Rect rr, Color c, Vector2 off)
        {
            if (rr.width <= 0f || rr.height <= 0f) return;
            var o = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(rr.x + off.x, rr.y + off.y, rr.width, rr.height), _dot);
            GUI.color = o;
        }

        /// <summary>(session 32 QA) Words fitted to a space in a style: as they are when they fit; else a line cut a letter at a time (`oneLine`), or
        /// whole words kept while their lines fit the height, then "…". Words with rich-text marks are left as they are.</summary>
        static string FitWords(string s, GUIStyle st, float w, float h, bool oneLine)
        {
            if (string.IsNullOrEmpty(s) || st == null || w <= 4f || s.IndexOf('<') >= 0) return s ?? "";
            bool Fits(string x) => oneLine ? st.CalcSize(new GUIContent(x)).x <= w : st.CalcHeight(new GUIContent(x), w) <= h;
            if (Fits(s)) return s;
            const string Tail = "…";
            if (oneLine)
            {
                for (int cut = s.Length - 1; cut > 0; cut--)
                {
                    string t = s.Substring(0, cut).TrimEnd(' ', '·', ',', ';', ':', '-') + Tail;
                    if (Fits(t)) return t;
                }
                return Tail;
            }
            for (int cut = s.LastIndexOf(' '); cut > 0; cut = s.LastIndexOf(' ', cut - 1))
            {
                string t = s.Substring(0, cut).TrimEnd(' ', ',', ';', ':', '-', '(') + Tail;
                if (Fits(t)) return t;
            }
            return s;
        }

        /// <summary>A card: a leaf of lighter paper, its family's colour down its edge and its tier's along its top, when and where, what (and how many).</summary>
        void TreeCard(int i, Rect c)
        {
            var nd = _tree.Nodes[i];
            bool cast = i == 0, hover = i == _treeHover, path = _treePath[i] == 1 || (i == _treeLatestCard && Time.unscaledTime - _treeLatestLit < 3f);   // ("the latest" lit a while)
            var ev = Event.current;
            Color fam = FamilyInk[nd.Family < FamilyInk.Length ? nd.Family : 0];
            Color power = _treePower >= 0 ? Powers[_treePower].Col : FamilyInk[Consequences.FamCast];
            float textX = c.x + 13f;
            if (ev.type == EventType.Repaint)
            {
                var o = GUI.color;
                GUI.color = new Color(0.25f, 0.15f, 0.06f, 0.20f); GUI.DrawTexture(new Rect(c.x + 2f, c.y + 3f, c.width, c.height), _dot);
                if (cast) { var dk = Color.Lerp(power, new Color(0.10f, 0.05f, 0.02f), 0.62f); GUI.color = new Color(dk.r, dk.g, dk.b, 1f); }
                else GUI.color = nd.More ? new Color(0.90f, 0.86f, 0.76f, 0.75f) : hover ? new Color(1f, 0.98f, 0.92f, 1f) : new Color(0.98f, 0.95f, 0.87f, 0.96f);
                GUI.DrawTexture(c, _dot);
                Color edge = path || hover ? Rubric : new Color(Faded.r, Faded.g, Faded.b, 0.45f);
                float t = path || hover ? 2f : 1f;
                GUI.color = cast ? new Color(power.r, power.g, power.b, 0.95f) : edge;
                GUI.DrawTexture(new Rect(c.x, c.y, c.width, t), _dot); GUI.DrawTexture(new Rect(c.x, c.yMax - t, c.width, t), _dot);
                GUI.DrawTexture(new Rect(c.x, c.y, t, c.height), _dot); GUI.DrawTexture(new Rect(c.xMax - t, c.y, t, c.height), _dot);
                if (!cast)
                {
                    GUI.color = new Color(fam.r, fam.g, fam.b, nd.More ? 0.45f : 1f); GUI.DrawTexture(new Rect(c.x, c.y, 6f, c.height), _dot);
                    GUI.color = TierColour((int)nd.Tier, 0.9f); GUI.DrawTexture(new Rect(c.x + 6f, c.y, c.width - 6f, 3f), _dot);
                }
                GUI.color = o;
                if (cast && _treePower >= 0)
                {
                    var seal = new Rect(c.x + 8f, c.center.y - 19f, 38f, 38f);
                    GUI.DrawTexture(seal, Ui("seal_on"));
                    Icon(Inset(seal, 9f), PowerIcons[_treePower], new Color(1f, 0.97f, 0.88f));
                }
            }
            if (cast && _treePower >= 0) textX = c.x + 54f;
            bool badge = !cast && !nd.More && !nd.Detail && nd.Count > 1;
            float bw = badge ? 34f : 0f;
            if (ev.type != EventType.Repaint) return;   // (words take no input: drawn on the repaint only)
            // (session 32 QA, s32pow4) the words fitted to the card - whole words then "…", the kicker's place shortened - where a third line had been drawn
            // cut through its middle ("their households dug out what t") and the place cut at the badge; the card's tip keeps every word
            float kw = c.xMax - textX - 8f - bw, tw = c.xMax - textX - 8f, th = c.height - 27f;
            GUIStyle ks = cast ? _tcKickCast : _tcKick, ts = cast ? _tcTitleCast : _tcTitle;
            if (_treeFitK[i] == null) _treeFitK[i] = FitWords(_treeKick[i], ks, kw, 0f, true);
            if (_treeFitT[i] == null) _treeFitT[i] = FitWords(nd.Title, ts, tw, th, false);
            GUI.Label(new Rect(textX, c.y + 7f, kw, 14f), _treeFitK[i], ks);
            GUI.Label(new Rect(textX, c.y + 23f, tw, th), nd.More ? "<i>" + _treeFitT[i] + "</i>" : _treeFitT[i], ts);
            if (badge)
            {
                var br = new Rect(c.xMax - bw - 6f, c.y + 6f, bw, 16f);
                var o = GUI.color; GUI.color = new Color(fam.r * 0.8f, fam.g * 0.8f, fam.b * 0.8f, 0.92f); GUI.DrawTexture(br, _dot); GUI.color = o;
                GUI.Label(br, "×" + nd.Count.ToString(CultureInfo.InvariantCulture), _tcBadge);
            }
            if (nd.Below > 0) GUI.Label(new Rect(c.xMax + 6f, c.center.y - 9f, TcGapX + 30f, 18f), "+" + nd.Below.ToString(CultureInfo.InvariantCulture) + "  ›", _tcBeyond);
        }

        /// <summary>Lays the tree out once (on opening, and when it is read again): a card's column its depth, the leaves one under another, each card level
        /// with the middle of its children; and each card's words, its tip and where a click on it goes.</summary>
        void TreeLayout(TC2.Core.Simulation.World w)
        {
            var t = _tree; int n = t.Nodes.Count;
            if (_treeCards.Length != n)
            {
                _treeCards = new Rect[n]; _treeKick = new string[n]; _treeTip = new string[n]; _treeGoWords = new string[n]; _treeFitK = new string[n]; _treeFitT = new string[n];
                _treeAct = new int[n]; _treeTab = new int[n]; _treePath = new int[n]; _treeTo = new long[n];
            }
            float next = TcPad;
            float Lay(int i)
            {
                var nd = t.Nodes[i];
                float y;
                if (nd.Children.Count == 0) { y = next; next += TcCardH + TcGapY; }
                else
                {
                    // (session 32, final) level with its first child: the tree reads from the top, each card at the head of its branch
                    y = Lay(nd.Children[0]);
                    for (int k = 1; k < nd.Children.Count; k++) Lay(nd.Children[k]);
                }
                _treeCards[i] = new Rect(TcPad + nd.Depth * (TcCardW + TcGapX), y, TcCardW, TcCardH);
                return y;
            }
            Lay(0);
            _treeW = TcPad * 2f + (t.MaxDepth + 1) * (TcCardW + TcGapX) - TcGapX + 70f;
            _treeH = next - TcGapY + TcPad;
            // the cast's power (its seal and colour)
            w.History.TryGet(t.Cast, out var ce);
            _treePower = -1;
            for (int k = 0; k < Powers.Length; k++) if ((int)Powers[k].Kind == ce.ArgA) _treePower = k;
            int years = (int)((t.LastTime - t.CastTime) / SimTime.Year);
            _treeSub = "year " + SimTime.YearOf(t.CastTime).ToString(CultureInfo.InvariantCulture) + "  ·  " + (t.Records == 0 ? "nothing yet that the world remembers" : t.Records.ToString("N0", CultureInfo.InvariantCulture) + (t.Records == 1 ? " record" : " records") + " so far" +
                       (years > 0 ? ", the latest " + years + (years == 1 ? " year" : " years") + " on" : "")) + (t.Capped ? " (the first " + Consequences.ScanCap.ToString("N0", CultureInfo.InvariantCulture) + " read)" : "");
            // (session 32, final) every record it has led to by when it came, in the tiers' colours; and the card that came last
            var wb = new StringBuilder();
            for (int k = 1; k < Consequences.Tiers; k++)
            {
                int c = t.TierRecords[k];
                if (c == 0) continue;
                wb.Append(wb.Length > 0 ? "      " : "").Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(TierColour(k, 1f))).Append('>')
                  .Append(Consequences.TierWords((ConsequenceTier)k).ToUpperInvariant()).Append("  ").Append(c.ToString("N0", CultureInfo.InvariantCulture)).Append("</color>");
            }
            _treeWhen = wb.ToString();
            _treeLatestCard = -1;
            long lastFirst = long.MinValue;
            for (int i = 1; i < n; i++) { var nd = t.Nodes[i]; if (!nd.More && nd.First > lastFirst) { lastFirst = nd.First; _treeLatestCard = i; } }
            if (_treeLatestCard > 0 && t.Nodes[_treeLatestCard].Tier < ConsequenceTier.Secondary) _treeLatestCard = -1;   // (a tree of one hour needs no way to its end)
            // the columns: the tiers each holds, from when to when
            for (int d = 0; d < _treeColMask.Length; d++) _treeColMask[d] = 0;
            var from = new long[_treeColMask.Length]; var to = new long[_treeColMask.Length];
            for (int d = 0; d < from.Length; d++) { from[d] = long.MaxValue; to[d] = long.MinValue; }
            foreach (var nd in t.Nodes)
            {
                if (nd.Depth >= _treeColMask.Length) continue;
                _treeColMask[nd.Depth] |= 1 << (int)nd.Tier;
                if (nd.More) continue;
                from[nd.Depth] = System.Math.Min(from[nd.Depth], nd.First - t.CastTime); to[nd.Depth] = System.Math.Max(to[nd.Depth], nd.First - t.CastTime);
            }
            for (int d = 0; d < _treeColHead.Length; d++)
            {
                int m = _treeColMask[d];
                var sb = new StringBuilder();
                for (int b = 0; b < Consequences.Tiers; b++)
                    if ((m & (1 << b)) != 0) sb.Append(sb.Length > 0 ? "  ·  " : "").Append(Consequences.TierWords((ConsequenceTier)b));
                _treeColHead[d] = sb.ToString().ToUpperInvariant();
                if (d > 0 && _treeColHead[d] == _treeColHead[d - 1].Replace("AND THEN - ", "")) _treeColHead[d] = "AND THEN - " + _treeColHead[d];   // (session 32: two columns "at once" read as one step)
                string a = from[d] == long.MaxValue ? "" : Consequences.Offset(from[d]), z = from[d] == long.MaxValue ? "" : Consequences.Offset(to[d]);
                if (a != z && a.EndsWith(" on", System.StringComparison.Ordinal)) a = a.Substring(0, a.Length - 3);   // ("5 days to 2 years on")
                _treeColSpan[d] = d == 0 ? "year " + SimTime.YearOf(t.CastTime).ToString(CultureInfo.InvariantCulture) : a == z ? a : a + " to " + z;
            }
            // each card: its kicker, where a click goes (its tip is written when it is first pointed at)
            for (int i = 0; i < n; i++)
            {
                var nd = t.Nodes[i];
                string where = nd.Place.Length > 0 && !nd.More ? "  ·  " + nd.Place : "";
                _treeKick[i] = (i == 0 ? "Your action  ·  " + nd.When : nd.More ? nd.When : nd.When + where).ToUpperInvariant();
                int act = ActRecord, tab = 0; long to1 = nd.Event; string go;
                w.History.TryGet(nd.Event, out var e);
                if (i == 0) { act = ActGo; go = "go there"; }
                else if (nd.More) { act = ActChapter; go = "every record of the cast, in the book"; }
                else if (nd.Count == 1 && !nd.Detail && e.Subject.Kind == EntityKind.Person && w.People.Exists(e.Subject.Id)) { act = ActPerson; to1 = e.Subject.Id; go = "open " + w.History.NameOf(e.Subject) + "'s life"; }
                else if (nd.Count == 1 && (e.Kind == HistoryKind.WorkBuilt || e.Kind == HistoryKind.WorkBegun) && e.Object.Kind == EntityKind.Building && w.Buildings.Exists(e.Object.Id)) { act = ActBuilding; to1 = e.Object.Id; go = "go to the " + TC2.Core.Places.Structures.Name((byte)e.ArgA); }
                else if (nd.Settlement > 0 && w.Settlements.Exists(nd.Settlement))
                {
                    act = ActPlace; to1 = nd.Settlement;
                    tab = nd.Family == Consequences.FamFood ? 4 : nd.Family == Consequences.FamPower ? PlaceRealmTab : nd.Family == Consequences.FamWorks ? 5 : 8;
                    go = "open " + nd.Place + "'s page, its " + PlaceTabs[tab].ToLowerInvariant();
                }
                else go = "the record and why, in the book";
                _treeAct[i] = act; _treeTab[i] = tab; _treeTo[i] = to1; _treeGoWords[i] = go; _treeTip[i] = null; _treeFitK[i] = null; _treeFitT[i] = null;
            }
        }

        /// <summary>A card's tip: its title, its record dated (or who and what it holds - the dead by name and age, the branches folded), what came of it
        /// beyond what is drawn, and where a click goes.</summary>
        string TreeTip(TC2.Core.Simulation.World w, int i)
        {
            var nd = _tree.Nodes[i];
            w.History.TryGet(nd.Event, out var e);
            var tip = new StringBuilder();
            tip.Append("<b>").Append(nd.Title).Append("</b>");
            if (i == 0) tip.Append('\n').Append(HistoryText.Dated(w, in e));
            else if (nd.More) { tip.Append("\nFolded here, the smaller branches:"); foreach (var f in nd.Folded) tip.Append("\n  ").Append(f); }
            else if (nd.Detail || nd.Members.Count == 1) tip.Append('\n').Append(HistoryText.Dated(w, in e));
            else
            {
                bool dead = nd.Kind == HistoryKind.PersonDied;
                tip.Append('\n').Append(dead ? "The dead: " : "");
                int shown = System.Math.Min(dead ? 16 : 8, nd.Members.Count);
                for (int k = 0; k < shown; k++) tip.Append(k == 0 ? "" : dead ? "; " : "\n").Append(Consequences.MemberLine(w, nd.Members[k]));
                if (nd.Members.Count > shown) tip.Append(dead ? "; " : "\n").Append("and ").Append(nd.Members.Count - shown).Append(" more");
            }
            if (nd.Below > 0) tip.Append("\n<i>").Append(nd.Below).Append(nd.Below == 1 ? " record more" : " records more").Append(" came of it, beyond what the tree draws</i>");
            if (i > 0 && !nd.More && nd.Reach - nd.Last >= SimTime.Year) tip.Append("\n<i>What came of it runs on to ").Append(Consequences.Offset(nd.Reach - _tree.CastTime)).Append(" from your hand</i>");   // (session 32, final)
            tip.Append("\n<color=#f2c35f>Click: ").Append(_treeGoWords[i]).Append("</color>");
            return tip.ToString();
        }

        /// <summary>A card clicked (-1: the cast's chapter in the book): what it is about opens, the tree closes.</summary>
        void TreeGo(TC2.Core.Simulation.World w, int i)
        {
            int act = i < 0 ? ActChapter : _treeAct[i];
            long to = i < 0 ? 0 : _treeTo[i];
            int tab = i < 0 ? 0 : _treeTab[i];
            long cast = _treeCast;
            int after = _tree != null ? _tree.CastsAfter : 99;
            _treeFromBook = false;
            CloseConsequences();
            AudioView.Ui("page");
            switch (act)
            {
                case ActPerson: ShowPerson((int)to, 5); break;
                case ActPlace: ShowPlace((int)to, tab); break;
                case ActBuilding: GoToBuilding(w, (int)to); break;
                case ActGo: if (w.History.TryGet(cast, out var ce) && Place(w, in ce, out double lat, out double lon) && Rig != null) Rig.FlyTo(lat, lon); break;
                case ActChapter:
                    if (after < 40) OpenBookAt(HistoryBook.YourActionsPart, after);
                    else { _book = true; _line = cast; _whyScroll = Vector2.zero; }
                    break;
                default: if (w.History.TryGet(to, out _)) { _book = true; _line = to; _whyScroll = Vector2.zero; } break;
            }
        }
    }
}
