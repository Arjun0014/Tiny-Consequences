using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 29: the person as a person) The person page as a leaf of the book with a face: a portrait drawn from what the world draws them in (their
    /// household's dye, the first's mantle and circlet in the realm's colour, a sworn man's cap and shield, a child smaller), their name and standing, badges
    /// for what marks them now (first, lord, sworn, sick, wounded, with child, a veteran, a master of a craft), and six sections: Now - what they are doing
    /// and why, where and until when, what they carry, their body in bars, their temperament; Family - a drawn tree (grandparents, parents, brothers and
    /// sisters, partners with each one's children, grandchildren), the dead in faded ink, every name a way to that person's page; Work - every skill as a
    /// bar of practice with how it is grown, the crafts they carry, what they are known for, the works they led; Standing and arms; Household - its home,
    /// grain, land, beasts and the people in it; Life - the chronicle (Core/History/Lives) as a dated timeline, its records' causes to walk. The words and
    /// numbers are the world's (Biography, Lives); nothing here writes it.
    /// </summary>
    public sealed partial class Hud
    {
        static readonly string[] PersonTabs = { "Now", "Family", "Work", "Standing & arms", "Household", "Life" };
        int _pgBuiltFor = -1; float _pgBuiltAt = -99f;
        List<Lives.Entry> _pgLife; Lives.Tree _pgTree; List<string[]> _pgTemper; List<Lives.Skill> _pgSkills;
        string _pgDeeds = "", _pgHome = "", _pgWorkText = "";
        List<string> _pgFollow = new List<string>();   // (session 29: whom their household follows and who follows it)
        readonly List<int> _pgBack = new List<int>();          // (the people this page came from: "back to ...")
        long _pgWhy;                                            // (a Life record whose causes are open)
        GUIStyle _pgLink, _pgBadge, _pgBox, _pgBoxDead, _pgBoxMe, _pgSmall, _pgBig, _pgItal, _pgBarLabel, _pgEntry, _pgEntryBig, _pgEntrySmall, _pgAge;
        Texture2D _pgBadgeTex, _pgTrack;
        readonly Dictionary<int, Texture2D> _portraits = new Dictionary<int, Texture2D>();
        readonly List<int> _portraitOrder = new List<int>();
        static Color KindInk(byte k) => k == Lives.Birth || k == Lives.Family ? new Color(0.30f, 0.45f, 0.22f) : k == Lives.Loss || k == Lives.Death ? new Color(0.20f, 0.17f, 0.15f)
            : k == Lives.Work ? new Color(0.62f, 0.42f, 0.12f) : k == Lives.Standing ? new Color(0.55f, 0.15f, 0.08f) : k == Lives.Fight ? new Color(0.62f, 0.12f, 0.10f)
            : k == Lives.Hardship ? new Color(0.40f, 0.34f, 0.26f) : k == Lives.Home ? new Color(0.20f, 0.36f, 0.55f) : new Color(0.45f, 0.40f, 0.33f);

        void PersonStyles()
        {
            BookStyles();
            if (_pgBadge != null) return;
            GUIStyle S(Font f, int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft, int l = 0, int rt = 0, int t = 0, int b = 0)
            {
                var st = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, fontStyle = fs, alignment = a, wordWrap = true, richText = true, padding = new RectOffset(l, rt, t, b), margin = new RectOffset(0, 0, 0, 0) };
                st.normal.textColor = c;
                return st;
            }
            _pgBadgeTex = Rounded(new Color(1f, 1f, 1f, 1f), new Color(0f, 0f, 0f, 0.25f));
            _pgBadge = new GUIStyle(GUI.skin.box) { font = _serif, fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset(8, 8, 2, 3), margin = new RectOffset(0, 5, 2, 2), border = new RectOffset(8, 8, 8, 8) };
            _pgBadge.normal.background = _pgBadgeTex; _pgBadge.normal.textColor = new Color(1f, 0.98f, 0.94f);
            var boxTex = Rounded(new Color(0.97f, 0.94f, 0.86f, 1f), new Color(0.45f, 0.30f, 0.16f, 0.85f));
            var boxHover = Rounded(new Color(1f, 0.97f, 0.88f, 1f), new Color(0.62f, 0.18f, 0.08f, 1f));
            _pgBox = new GUIStyle(GUI.skin.button) { font = _serif, fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true, padding = new RectOffset(4, 4, 2, 2), border = new RectOffset(8, 8, 8, 8) };
            _pgBox.normal.background = boxTex; _pgBox.hover.background = boxHover; _pgBox.active.background = boxHover;
            _pgBox.normal.textColor = Ink; _pgBox.hover.textColor = Rubric; _pgBox.active.textColor = Rubric;
            _pgBoxDead = new GUIStyle(_pgBox);
            _pgBoxDead.normal.background = Rounded(new Color(0.88f, 0.85f, 0.79f, 1f), new Color(0.45f, 0.40f, 0.34f, 0.6f));
            _pgBoxDead.normal.textColor = Faded;
            _pgBoxMe = new GUIStyle(_pgBox) { fontStyle = FontStyle.Bold };
            _pgBoxMe.normal.background = Rounded(new Color(0.99f, 0.90f, 0.70f, 1f), Rubric);
            _pgSmall = S(_serif, 13, Faded, FontStyle.Italic);
            _pgBig = S(_serif, 19, Ink, FontStyle.Bold, t: 2, b: 2);
            _pgItal = S(_serif, 16, new Color(0.30f, 0.23f, 0.16f), FontStyle.Italic, b: 4);
            _pgBarLabel = S(_display, 15, Ink, a: TextAnchor.MiddleLeft);
            _pgEntry = S(_serif, 15, Ink, l: 2, t: 2, b: 3); _pgEntry.hover.textColor = Rubric;
            _pgEntryBig = S(_serif, 16, Ink, FontStyle.Bold, l: 2, t: 2, b: 3); _pgEntryBig.hover.textColor = Rubric;
            _pgEntrySmall = S(_serif, 14, Faded, l: 2, t: 2, b: 3); _pgEntrySmall.hover.textColor = Rubric;
            _pgAge = S(_serif, 12, Faded, FontStyle.Italic, a: TextAnchor.UpperRight, rt: 8);
            _pgTrack = Solid(new Color(0.55f, 0.45f, 0.32f, 0.22f));
            _pgLink = S(_serif, 15, Rubric, FontStyle.Italic, a: TextAnchor.MiddleLeft, l: 2, rt: 12, t: 2, b: 2); _pgLink.wordWrap = false; _pgLink.hover.textColor = Ink;
        }

        /// <summary>The person page: the header with the portrait and badges, the sections, the chosen section.</summary>
        void PersonSheet(Rect r, TC2.Core.Simulation.World w)
        {
            PersonStyles();
            int pid = _selected;
            ref var p = ref w.People.Get(pid);
            if (_pgBuiltFor != pid || Time.unscaledTime - _pgBuiltAt > 1.5f)
            {
                if (_pgBuiltFor != pid) { _pgWhy = 0; _pageScroll = Vector2.zero; }
                _pgBuiltFor = pid; _pgBuiltAt = Time.unscaledTime;
                _pageTitle = Biography.Title(w, pid);
                _pageStanding = Biography.Standing(w, pid);
                _pgLife = Lives.Chronicle(w, pid);
                _pgTree = Lives.KinOf(w, pid);
                _pgTemper = Lives.Temperament(in p);
                _pgSkills = Lives.Skills(in p);
                _pgDeeds = Biography.Deeds(w, pid);
                _pgHome = Biography.Home(w, pid);
                _pgWorkText = Biography.Work(w, pid);
                _pgFollow = Lives.Following(w, pid);
            }
            if (Event.current.type == EventType.Repaint)
            {
                Binding(new Rect(r.x - 8f, r.y - 7f, r.width + 16f, r.height + 14f));
                GUI.DrawTexture(r, _leaf);
            }
            GUILayout.BeginArea(new Rect(r.x + 24f, r.y + 14f, r.width - 40f, r.height - 22f));
            // the header: kicker, back, close; the portrait beside the name, what they are and their badges
            GUILayout.BeginHorizontal();
            Kicker(p.Alive ? "A Life" : "A Life Lived");
            if (_pgBack.Count > 0)
            {
                int back = _pgBack[_pgBack.Count - 1];
                if (w.People.Exists(back) && GUILayout.Button("‹  back to " + Given(w, back), _bkNav)) { _pgBack.RemoveAt(_pgBack.Count - 1); _selected = back; _pageTab = 0; return; }
            }
            GUILayout.FlexibleSpace();
            if (p.Alive && GUILayout.Button(_follow == pid ? "stop following" : "follow", _bkNav, GUILayout.Width(110))) ToggleFollow(w, pid);
            if (GUILayout.Button("close  ×", _bkNav, GUILayout.Width(84))) { _personPage = false; _pgBack.Clear(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Rect face = GUILayoutUtility.GetRect(78f, 98f, GUILayout.Width(78f), GUILayout.Height(98f));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(face, Portrait(w, pid), ScaleMode.StretchToFill, true);
            }
            GUILayout.Space(12);
            GUILayout.BeginVertical();
            GUILayout.Label(_pageTitle + (p.Alive ? "" : "  †"), _pgTitle);
            GUILayout.Label(Subline(w, pid), _pgItal);
            if (_pageStanding.Length > 0) GUILayout.Label("<color=#8c2614>" + Cap(_pageStanding) + "</color>", _pgSub);
            Badges(w, pid);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            PersonLinks(w, pid);
            // the sections
            GUILayout.BeginHorizontal();
            for (int i = 0; i < PersonTabs.Length; i++)
            {
                if (i > 0) GUILayout.Label("·", _pgDot);
                if (GUILayout.Button(PersonTabs[i], i == _pageTab ? _pgTabSel : _pgTab)) { _pageTab = i; _pageScroll = Vector2.zero; _pgWhy = 0; AudioView.Ui("leaf"); }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            Rule(0f, 1f);
            _pageScroll = GUILayout.BeginScrollView(_pageScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            float cw = r.width - 70f;
            switch (_pageTab)
            {
                case 0: NowSection(w, pid, cw); break;
                case 1: FamilySection(w, pid, cw); break;
                case 2: WorkSection(w, pid, cw); break;
                case 3: TextSection(_pgDeeds); if (_pgFollow.Count > 0) { GUILayout.Label("Among the households", _pgHead); foreach (var f in _pgFollow) GUILayout.Label(f, _pgBody); } break;
                case 4: HouseholdSection(w, pid); break;
                default: LifeSection(w, pid, cw); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// (session 29) The card a click on a person opens - a small leaf: their portrait, name and standing, what they are doing in bold and why under it,
        /// where, their health and food as bars, the badge of what marks them, the last thing their life records; Follow, their life, their village.
        /// </summary>
        void PersonCard(Rect r, TC2.Core.Simulation.World w)
        {
            PersonStyles();
            int pid = _selected;
            ref var p = ref w.People.Get(pid);
            if (_pgBuiltFor != pid || Time.unscaledTime - _pgBuiltAt > 1.5f)
            {
                _pgBuiltFor = pid; _pgBuiltAt = Time.unscaledTime;
                _pageTitle = Biography.Title(w, pid); _pageStanding = Biography.Standing(w, pid);
                _pgLife = Lives.Chronicle(w, pid); _pgTree = Lives.KinOf(w, pid); _pgTemper = Lives.Temperament(in p); _pgSkills = Lives.Skills(in p);
                _pgDeeds = Biography.Deeds(w, pid); _pgHome = Biography.Home(w, pid); _pgWorkText = Biography.Work(w, pid);
            }
            if (Event.current.type == EventType.Repaint)
            {
                Binding(new Rect(r.x - 7f, r.y - 6f, r.width + 14f, r.height + 12f));
                GUI.DrawTexture(r, _leaf);
            }
            GUILayout.BeginArea(new Rect(r.x + 14f, r.y + 10f, r.width - 26f, r.height - 16f));
            GUILayout.BeginHorizontal();
            Rect face = GUILayoutUtility.GetRect(56f, 70f, GUILayout.Width(56f), GUILayout.Height(70f));
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(face, Portrait(w, pid), ScaleMode.StretchToFill, true);
            GUILayout.Space(8);
            GUILayout.BeginVertical();
            GUILayout.Label(_pageTitle + (p.Alive ? "" : "  †"), _pgBig);
            if (_pageStanding.Length > 0) GUILayout.Label("<color=#8c2614>" + Cap(_pageStanding) + "</color>", _pgSmall);
            GUILayout.Label(Subline(w, pid), _pgSmall);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            float cw = r.width - 30f;
            if (p.Alive)
            {
                string doing = HistoryText.Doing(w, in p);
                int colon = doing.IndexOf(": ", System.StringComparison.Ordinal);
                GUILayout.Label(Cap(colon > 0 ? doing.Substring(0, colon) : doing), _pgEntryBig);
                if (colon > 0) GUILayout.Label("Why: " + doing.Substring(colon + 2), _pgSmall);
                Bar("Health", p.Health, p.Sick != 0 && p.SickPhase != TC2.Core.Health.Disease.Latent ? "sick with " + w.History.NameOf(new EntityRef(EntityKind.Pathogen, p.Sick)) : p.Hurt >= 0.15f ? "wounded" : p.Health >= 0.9f ? "in good health" : "unwell", new Color(0.32f, 0.52f, 0.25f), cw);
                Bar("Fed", p.Nutrition, p.Nutrition >= 0.8f ? "well fed" : p.Nutrition >= 0.5f ? "hungry" : "starving", new Color(0.70f, 0.52f, 0.18f), cw);
            }
            else if (p.DeathEvent != 0 && w.History.TryGet(p.DeathEvent, out var de)) GUILayout.Label(HistoryText.Describe(w, de), _pgEntry);
            if (_pgLife != null && _pgLife.Count > 0) { var last = _pgLife[_pgLife.Count - 1]; GUILayout.Label("<color=#8c2614>" + SimTime.YearOf(last.Time) + "</color>  " + last.Text, _pgSmall); }
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (p.Alive && GUILayout.Button(_follow == pid ? "stop following" : "follow", _bkNav)) ToggleFollow(w, pid);
            if (GUILayout.Button("their life  ›", _bkNav)) { _personPage = true; _pageTab = 0; _pgBack.Clear(); _placeSid = 0; AudioView.Ui("page"); }
            if (Event.current.type == EventType.Repaint) { var lb = GUILayoutUtility.GetLastRect(); _lifeBtnRect = new Rect(r.x + 14f + lb.x, r.y + 10f + lb.y, lb.width, lb.height); }
            if (p.Home != 0 && GUILayout.Button("their village  ›", _bkNav)) ShowPlace(p.Home);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("×", _bkNav, GUILayout.Width(26))) { _selected = 0; if (_follow == pid) { } }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>"born in year 266 in Fostead · 34 years · of the Theiais, Kriford" - who they are in one line.</summary>
        static string Subline(TC2.Core.Simulation.World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            int born = Biography.BornAt(w, pid);
            string s = p.Born < 0 ? "one of the first people of the land" : "born in year " + SimTime.YearOf(p.Born) + (born != 0 ? " in " + w.History.NameOf(EntityRef.Settlement(born)) : "");
            if (!p.Alive) return s + "; died in year " + SimTime.YearOf(p.Died) + ", aged " + ((int)p.AgeYears(p.Died)).ToString(CultureInfo.InvariantCulture);
            if (p.Household != 0 && w.Households.Exists(p.Household)) s += "  ·  of the " + HistoryText.Plural(w.History.NameOf(EntityRef.Household(p.Household)));
            if (p.Home != 0) s += ", " + w.History.NameOf(EntityRef.Settlement(p.Home));
            return s;
        }

        /// <summary>What marks them now, as small badges in colour: their standing, their body's troubles, their road, their craft.</summary>
        void Badges(TC2.Core.Simulation.World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            var list = new List<KeyValuePair<string, Color>>();
            Color realm = new Color(0.55f, 0.15f, 0.08f), ill = new Color(0.45f, 0.52f, 0.20f), hurt = new Color(0.62f, 0.12f, 0.10f), craft = new Color(0.55f, 0.40f, 0.12f), kin = new Color(0.28f, 0.42f, 0.26f), road = new Color(0.20f, 0.36f, 0.55f);
            if (p.Alive && w.Settlements.Exists(p.Home))
            {
                ref var s = ref w.Settlements.Get(p.Home);
                if (s.Head == pid)
                {
                    int held = 0;
                    for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) { ref var os = ref w.Settlements.Get(o); if (os.Lord == p.Home && os.Inhabited) held++; }
                    list.Add(new KeyValuePair<string, Color>(held > 0 ? "Lord of " + held + (held == 1 ? " village" : " villages") : "First of " + w.History.NameOf(EntityRef.Settlement(p.Home)), realm));
                    int men = 0; var res = w.Residents(p.Home);
                    for (int i = 0; i < res.Count; i++) if (w.People.Get(res[i]).Sworn == pid) men++;
                    if (men > 0) list.Add(new KeyValuePair<string, Color>(men + " sworn " + (men == 1 ? "man" : "men"), realm));
                }
                else if (p.Sworn != 0 && p.Sworn == s.Head) list.Add(new KeyValuePair<string, Color>("Sworn man", realm));
            }
            if (!p.Alive) list.Add(new KeyValuePair<string, Color>("Died year " + SimTime.YearOf(p.Died), new Color(0.25f, 0.22f, 0.20f)));
            else
            {
                if (p.Sick != 0 && p.SickPhase != TC2.Core.Health.Disease.Latent) list.Add(new KeyValuePair<string, Color>("Sick", ill));
                if (p.Hurt >= 0.15f) list.Add(new KeyValuePair<string, Color>(p.Hurt >= 0.4f ? "Badly wounded" : "Wounded", hurt));
                if (p.Nutrition < 0.5f) list.Add(new KeyValuePair<string, Color>("Starving", hurt));
                else if (p.Nutrition < 0.8f) list.Add(new KeyValuePair<string, Color>("Hungry", craft));
                if (p.PregDue != 0) list.Add(new KeyValuePair<string, Color>("With child", kin));
                if (p.At == 0) list.Add(new KeyValuePair<string, Color>("On the road", road));
                if (p.Voyage != 0) list.Add(new KeyValuePair<string, Color>("At sea", road));
                if (p.Band != 0) list.Add(new KeyValuePair<string, Color>("With a band", hurt));
            }
            if (p.Fights >= 3) list.Add(new KeyValuePair<string, Color>("Veteran of " + p.Fights + " fights", hurt));
            if (p.Born < 0) list.Add(new KeyValuePair<string, Color>("Among the first people", kin));
            float best = 0f; string master = null;
            foreach (var sk in _pgSkills ?? new List<Lives.Skill>()) if (sk.Value >= 0.9f && sk.Value > best) { best = sk.Value; master = sk.Name; }
            if (master != null) list.Add(new KeyValuePair<string, Color>("Master of " + master.ToLowerInvariant(), craft));
            if (list.Count == 0) return;
            GUILayout.BeginHorizontal();
            var old = GUI.backgroundColor;
            foreach (var kv in list) { GUI.backgroundColor = kv.Value; GUILayout.Label(kv.Key, _pgBadge); }
            GUI.backgroundColor = old;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>A bar of 0..1 with its label and its words.</summary>
        void Bar(string label, float v, string words, Color c, float cw)
        {
            Rect row = GUILayoutUtility.GetRect(cw, 24f);
            GUI.Label(new Rect(row.x, row.y, 130f, row.height), label, _pgBarLabel);
            var track = new Rect(row.x + 132f, row.y + 8f, Mathf.Min(220f, cw * 0.36f), 8f);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(track, _pgTrack);
                GUI.color = c; GUI.DrawTexture(new Rect(track.x, track.y, track.width * Mathf.Clamp01(v), track.height), _dot); GUI.color = Color.white;
            }
            GUI.Label(new Rect(track.xMax + 12f, row.y + 1f, row.width - track.xMax + row.x - 12f, row.height), words, _pgSmall);
        }

        void NowSection(TC2.Core.Simulation.World w, int pid, float cw)
        {
            ref var p = ref w.People.Get(pid);
            if (!p.Alive)
            {
                GUILayout.Label("Died in year " + SimTime.YearOf(p.Died) + ", aged " + ((int)p.AgeYears(p.Died)).ToString(CultureInfo.InvariantCulture) + ".", _pgBig);
                if (p.DeathEvent != 0 && w.History.TryGet(p.DeathEvent, out var de)) GUILayout.Label(HistoryText.Describe(w, de), _pgItal);
                GUILayout.Label(Epitaph(w, pid), _pgBody);
                Rule(0f, 0.4f);
                int heir = Heir(w, pid);
                if (heir != 0 && GUILayout.Button("Go on with " + Given(w, heir) + Kinship(w, pid, heir) + "  ›", _bkNav)) GoTo(heir);
                GUILayout.Label("Their life is told under Life; their family under Family.", _pgSmall);
                PersonWhys(w, pid, cw);
                return;
            }
            // what they do and why, where, until when, what they carry
            string doing = HistoryText.Doing(w, in p);
            int colon = doing.IndexOf(": ", System.StringComparison.Ordinal);
            string what = colon > 0 ? doing.Substring(0, colon) : doing, why = colon > 0 ? doing.Substring(colon + 2) : "";
            GUILayout.Label(Cap(what), _pgBig);
            if (why.Length > 0) GUILayout.Label("Why: " + why, _pgItal);
            string where = p.At == 0 ? "On the road" + (p.Dest != 0 ? " to " + w.History.NameOf(EntityRef.Settlement(p.Dest)) : "") : p.At == p.Home ? "At home in " + w.History.NameOf(EntityRef.Settlement(p.At)) : "Visiting " + w.History.NameOf(EntityRef.Settlement(p.At));
            GUILayout.Label(where + (p.T1 > w.Now ? ", until " + UntilWords(w.Now, p.T1) : "") + ".", _pgBody);
            if (p.CargoQty > 0f) GUILayout.Label(CarryWords(w, in p), _pgBody);
            GUILayout.Space(6);
            GUILayout.Label("Body", _pgHead);
            Bar("Health", p.Health, p.Health >= 0.9f ? "in good health" : p.Health >= 0.6f ? "unwell" : p.Health >= 0.3f ? "ailing" : "near death", new Color(0.32f, 0.52f, 0.25f), cw);
            Bar("Fed", p.Nutrition, p.Nutrition >= 0.8f ? "well fed" : p.Nutrition >= 0.5f ? "hungry" : "starving", new Color(0.70f, 0.52f, 0.18f), cw);
            Bar("Reserve", Mathf.Clamp01(p.Reserve / 40f), p.Reserve >= 30f ? "flesh to spare for a month or more" : p.Reserve >= 14f ? "some weeks' flesh to spare" : p.Reserve >= 5f ? "a few days' flesh to spare" : "nothing to spare in the body", new Color(0.62f, 0.38f, 0.22f), cw);
            if (p.Hurt > 0.01f) Bar("Wounds", p.Hurt, p.Hurt >= 0.4f ? "badly wounded and abed" : p.Hurt >= 0.15f ? "wounded" : "lightly hurt", new Color(0.62f, 0.12f, 0.10f), cw);
            if (p.Sick != 0) GUILayout.Label((p.SickPhase == TC2.Core.Health.Disease.Latent ? "Carrying " : "Sick with ") + w.History.NameOf(new EntityRef(EntityKind.Pathogen, p.Sick)) + (p.SickPhase == TC2.Core.Health.Disease.Latent ? " unknowing" : "") + (p.SickUntil > w.Now ? ", " + (p.SickPhase == TC2.Core.Health.Disease.Latent ? "falling ill " : "abed ") + UntilWords(w.Now, p.SickUntil) : "") + ".", _pgBody);
            if (p.PregDue != 0) GUILayout.Label("With child, due " + (p.PregDue > w.Now ? UntilWords(w.Now, p.PregDue) : "now") + ".", _pgBody);
            if (p.NursingUntil > w.Now) GUILayout.Label("Nursing a baby.", _pgBody);
            if (_pgTemper != null && _pgTemper.Count > 0)
            {
                GUILayout.Label("Temperament", _pgHead);
                GUILayout.Label("born with it; the world's choices read it every day", _pgSmall);
                foreach (var t in _pgTemper) GUILayout.Label("<color=#8c2614>" + t[0] + "</color>  -  " + t[1], _pgBody);
            }
            PersonWhys(w, pid, cw);
            if (_pgLife != null && _pgLife.Count > 0)
            {
                GUILayout.Label("Of late", _pgHead);
                for (int i = Mathf.Max(0, _pgLife.Count - 3); i < _pgLife.Count; i++) LifeRow(w, _pgLife[i], cw);
            }
        }

        /// <summary>(session 31) The way from a person to what is theirs - their home, where they work now, their village, their realm - one row, each a click away.</summary>
        void PersonLinks(TC2.Core.Simulation.World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            if (!p.Alive) return;
            GUILayout.BeginHorizontal();
            int home = p.Household != 0 && w.Households.Exists(p.Household) ? w.Households.Get(p.Household).Building : 0;
            if (home != 0 && w.Buildings.Exists(home) && GUILayout.Button("their home  ›", _pgLink)) GoToBuilding(w, home);
            int place = p.At != 0 ? p.At : p.Home;
            int work = WorkplaceOf(w, in p);
            if (work != 0 && GUILayout.Button("where they work  ›", _pgLink)) GoToBuilding(w, work);
            if (p.Home != 0 && w.Settlements.Exists(p.Home) && GUILayout.Button("their village  ›", _pgLink)) ShowPlace(p.Home, PlaceNowTab);
            if (p.Home != 0 && w.Settlements.Exists(p.Home) && (w.Settlements.Get(p.Home).Lord != 0 || w.Conflict.PolitySize(p.Home) > 0) && GUILayout.Button("their realm  ›", _pgLink)) ShowPlace(p.Home, PlaceRealmTab);
            if (Presence.Of(w, in p, Host.RenderTime, out var at, out _, out _, out _) && GUILayout.Button("see them  ›", _pgLink)) { Rig.FlyTo(at, System.Math.Min(Rig.Distance - w.Planet.Radius, 90.0), 2.2f); }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>The work a person is at this moment, by its kind, in the village they are in (0 none).</summary>
        static int WorkplaceOf(TC2.Core.Simulation.World w, in Person p)
        {
            int kind = -1;
            switch (p.Act)
            {
                case ActivityKind.Smith: kind = TC2.Core.Places.Structures.Smithy; break;
                case ActivityKind.Weave: kind = TC2.Core.Places.Structures.Workshop; break;
                case ActivityKind.Pot: kind = TC2.Core.Places.Structures.Kiln; break;
                case ActivityKind.Quarry: kind = TC2.Core.Places.Structures.Quarry; break;
                case ActivityKind.Mine: kind = TC2.Core.Places.Structures.Mine; break;
                case ActivityKind.Burn: kind = TC2.Core.Places.Structures.CharHearth; break;
                case ActivityKind.Train: kind = TC2.Core.Places.Structures.Ground; break;
                case ActivityKind.Thresh: kind = TC2.Core.Places.Structures.Barn; break;
                default: return 0;
            }
            int sid = p.At != 0 ? p.At : p.Home;
            if (sid == 0 || !w.Settlements.Exists(sid)) return 0;
            if (p.Act == ActivityKind.Smith && p.WhyArg != 0 && w.Buildings.Exists(p.WhyArg)) return p.WhyArg;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++) { ref var b = ref w.Buildings.Get(bl[i]); if (b.Kind == kind && b.Done != 0) return bl[i]; }
            return 0;
        }

        /// <summary>A building's page, the camera flying to it.</summary>
        void GoToBuilding(TC2.Core.Simulation.World w, int bid)
        {
            AudioView.Ui("select");
            _personPage = false; _placeSid = 0; _building = bid; _bScroll = Vector2.zero;
            ref var b = ref w.Buildings.Get(bid);
            if (w.Settlements.Exists(b.Settlement)) { var at = w.Settlements.Get(b.Settlement).LocalToSphere(b.X, b.Y, w.Planet.Radius); Rig.FlyTo(at, System.Math.Min(Rig.Distance - w.Planet.Radius, 240.0), 2.2f); }
        }

        /// <summary>(session 31) Follows someone (the camera flies down to them, then keeps with them as they go about their day), or stops.</summary>
        void ToggleFollow(TC2.Core.Simulation.World w, int pid)
        {
            AudioView.Ui("select");
            if (_follow == pid) { _follow = 0; return; }
            _follow = pid;
            if (Presence.Of(w, in w.People.Get(pid), Host.RenderTime, out var d, out _, out _, out _)) Rig.FlyTo(d, System.Math.Min(Rig.Distance - w.Planet.Radius, 90.0), 2.2f);
            else Rig.FlyTo(w.Agents.PositionAt(w.People.Get(pid), Host.RenderTime), System.Math.Min(Rig.Distance - w.Planet.Radius, 240.0), 2.2f);
        }

        /// <summary>(session 31) A completed life in a few words, from what the world kept: the years lived, whom they left, what they were best at, what marked them.</summary>
        string Epitaph(TC2.Core.Simulation.World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            var parts = new List<string>();
            int age = (int)p.AgeYears(p.Died);
            parts.Add(age < 5 ? "A short life." : age < 16 ? "A child's few years." : age < 40 ? "A life cut short." : age < 65 ? "A full life for its time." : "A long life.");
            int kids = 0;
            if (_pgTree != null) foreach (var l in _pgTree.ChildrenOf) kids += l.Count;
            int living = 0;
            if (_pgTree != null) foreach (var l in _pgTree.ChildrenOf) foreach (int c in l) if (w.People.Exists(c) && w.People.Get(c).Alive) living++;
            int grand = _pgTree != null ? _pgTree.Grandchildren.Count : 0;
            if (kids > 0) parts.Add(Cap(Number(kids)) + (kids == 1 ? " child" : " children") + (living > 0 && living < kids ? ", " + Number(living) + " of them living still" : living == 0 ? ", none of them living now" : "") + (grand > 0 ? "; " + Number(grand) + (grand == 1 ? " grandchild" : " grandchildren") : "") + ".");
            else if (age >= 16) parts.Add("No children to carry them on.");
            float best = 0f; string craft = null;
            if (_pgSkills != null) foreach (var sk in _pgSkills) if (sk.Value > best && sk.Name != "Fighting") { best = sk.Value; craft = sk.Name; }
            if (craft != null && best >= 0.3f && age >= 14) parts.Add(best >= 0.9f ? "A master of " + craft.ToLowerInvariant() + "." : best >= 0.6f ? "Skilled at " + craft.ToLowerInvariant() + "." : "Worked at " + craft.ToLowerInvariant() + ".");
            if (p.Fights >= 3) parts.Add("Stood in " + Number(p.Fights) + " fights.");
            return string.Join(" ", parts.ToArray());
        }

        static string Number(int n)
        {
            string[] words = { "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" };
            return n >= 0 && n < words.Length ? words[n] : n.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>(session 29) The questions their state and records can answer - why they are first, sworn, sick, wounded, why they died - each opening
        /// its story from the root.</summary>
        void PersonWhys(TC2.Core.Simulation.World w, int pid, float cw)
        {
            var asks = Whys.OfPerson(w, pid);
            if (asks.Count == 0) return;
            GUILayout.Label("Why?", _pgHead);
            foreach (var q in asks) WhyToggle(w, q.Question, q.Event, cw);
        }

        static string UntilWords(long now, long t)
        {
            long dNow = SimTime.FloorDiv(now, SimTime.Day), dT = SimTime.FloorDiv(t, SimTime.Day);
            int h = SimTime.HourOfDay(t), m = SimTime.MinuteOfHour(t);
            string clock = (h < 10 ? "0" : "") + h.ToString(CultureInfo.InvariantCulture) + ":" + (m < 10 ? "0" : "") + m.ToString(CultureInfo.InvariantCulture);
            return dT == dNow ? clock : dT == dNow + 1 ? "tomorrow at " + clock : "in " + (dT - dNow).ToString(CultureInfo.InvariantCulture) + " days";
        }

        static string CarryWords(TC2.Core.Simulation.World w, in Person p)
        {
            string what = p.Cargo == Trade.Metal ? " kg of copper" : p.Cargo == Trade.Iron ? " kg of iron" : p.Cargo == Trade.Grain ? " days' grain" : p.Cargo == Trade.Fibre ? " kg of fibre" : p.Cargo == Trade.Wares ? " wares" : p.Cargo == Trade.Pots ? " kg of pots and jars" : " hides";
            string to = p.CargoTo == Trade.ToLord || p.CargoTo == Trade.ToTenth || p.CargoTo == Trade.TenthAboard ? " - the lord's tenth" : p.CargoTo == Trade.AsGift ? " - a gift of grain to a famine" : p.CargoTo == Trade.AsReturn ? " - grain given back" : "";
            return "Carrying " + p.CargoQty.ToString("0.#", CultureInfo.InvariantCulture) + what + to + ".";
        }

        /// <summary>The page goes to another person (a relative clicked): the one it came from kept for "back".</summary>
        void GoTo(int pid)
        {
            if (pid == 0 || pid == _selected) return;
            if (_selected != 0) { _pgBack.Add(_selected); if (_pgBack.Count > 12) _pgBack.RemoveAt(0); }
            _selected = pid; _personPage = true; _pageScroll = Vector2.zero; _pgWhy = 0;
        }

        /// <summary>A person in the tree: their given name, years, alive or dead (faded, †); a click goes to them.</summary>
        bool KinBox(TC2.Core.Simulation.World w, Rect r, int pid, string role, bool me = false)
        {
            if (pid == 0 || !w.People.Exists(pid))
            {
                if (Event.current.type == EventType.Repaint && role != null) GUI.Label(r, "<i>" + role + "\nunknown</i>", _pgSmall);
                return false;
            }
            ref var q = ref w.People.Get(pid);
            string years = q.Alive ? (q.Born < 0 ? "a first settler" : "b. " + SimTime.YearOf(q.Born)) + ", " + ((int)q.AgeYears(w.Now)).ToString(CultureInfo.InvariantCulture)
                                   : (q.Born < 0 ? "" : SimTime.YearOf(q.Born) + "–") + SimTime.YearOf(q.Died) + " †";
            string text = (role != null ? "<size=10><i>" + role + "</i></size>\n" : "") + Given(w, pid) + "\n<size=11>" + years + "</size>";
            if (Event.current.type == EventType.Repaint)
            {
                Color c = WorldObjectsView.PageLivery(w, pid, out _);
                GUI.color = q.Alive ? c : Color.Lerp(c, new Color(0.6f, 0.58f, 0.55f), 0.7f);
                GUI.DrawTexture(new Rect(r.x + 4f, r.y + 4f, 5f, r.height - 8f), _dot);
                GUI.color = Color.white;
            }
            bool click = GUI.Button(r, text, me ? _pgBoxMe : q.Alive ? _pgBox : _pgBoxDead);
            if (click && !me) GoTo(pid);
            return click;
        }

        static void Line(Rect a, Rect b, Texture2D dot)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = new Color(0.45f, 0.30f, 0.16f, 0.65f);
            float ax = a.center.x, ay = a.yMax, bx = b.center.x, by = b.y, mid = (ay + by) * 0.5f;
            GUI.DrawTexture(new Rect(ax - 0.75f, ay, 1.5f, mid - ay), dot);
            GUI.DrawTexture(new Rect(Mathf.Min(ax, bx) - 0.75f, mid - 0.75f, Mathf.Abs(bx - ax) + 1.5f, 1.5f), dot);
            GUI.DrawTexture(new Rect(bx - 0.75f, mid, 1.5f, by - mid), dot);
            GUI.color = Color.white;
        }

        void FamilySection(TC2.Core.Simulation.World w, int pid, float cw)
        {
            var t = _pgTree;
            if (t == null) return;
            const float bw = 112f, bh = 52f, gap = 8f, row = 78f;
            // the rows: grandparents, parents, the person with brothers and sisters and partners, children by partner, grandchildren
            int kidsTotal = 0; foreach (var l in t.ChildrenOf) kidsTotal += l.Count;
            int perRow = Mathf.Max(1, (int)((cw + gap) / (bw + gap)));
            int kidRows = Mathf.Max(1, Mathf.CeilToInt(kidsTotal / (float)perRow));
            float h = row * 3f + (kidsTotal > 0 ? row * kidRows : 0f) + 40f;
            Rect area = GUILayoutUtility.GetRect(cw, h);
            float x0 = area.x, y0 = area.y, mid = area.x + cw * 0.5f;
            // grandparents in four even slots, each parent under its own pair (session 29, s29book1: the inner two had overlapped)
            float slot = bw + gap, g0 = mid - 2f * slot + gap * 0.5f;
            Rect mm = new Rect(g0, y0, bw, bh * 0.82f), mf = new Rect(g0 + slot, y0, bw, bh * 0.82f);
            Rect fm = new Rect(g0 + 2f * slot, y0, bw, bh * 0.82f), ff = new Rect(g0 + 3f * slot, y0, bw, bh * 0.82f);
            Rect mo = new Rect((mm.x + mf.x) * 0.5f, y0 + row, bw, bh), fa = new Rect((fm.x + ff.x) * 0.5f, y0 + row, bw, bh);
            Rect me = new Rect(mid - bw * 0.5f, y0 + row * 2f, bw, bh);
            if (t.Mother != 0) { if (t.MotherMother != 0) Line(mm, mo, _dot); if (t.MotherFather != 0) Line(mf, mo, _dot); }
            if (t.Father != 0) { if (t.FatherMother != 0) Line(fm, fa, _dot); if (t.FatherFather != 0) Line(ff, fa, _dot); }
            if (t.Mother != 0) Line(mo, me, _dot);
            if (t.Father != 0) Line(fa, me, _dot);
            if (t.MotherMother != 0) KinBox(w, mm, t.MotherMother, "grandmother");
            if (t.MotherFather != 0) KinBox(w, mf, t.MotherFather, "grandfather");
            if (t.FatherMother != 0) KinBox(w, fm, t.FatherMother, "grandmother");
            if (t.FatherFather != 0) KinBox(w, ff, t.FatherFather, "grandfather");
            KinBox(w, mo, t.Mother, "mother");
            KinBox(w, fa, t.Father, "father");
            KinBox(w, me, pid, null, true);
            // brothers and sisters to the left of the person, partners to the right
            float sx = me.x - gap - bw;
            for (int i = t.Siblings.Count - 1, shown = 0; i >= 0 && shown < 3; i--, shown++)
            {
                if (sx < x0) break;
                var sr = new Rect(sx, me.y, bw, bh);
                bool girl = w.People.Get(t.Siblings[i]).IsFemale;
                KinBox(w, sr, t.Siblings[i], (t.Half[i] ? "half-" : "") + (girl ? "sister" : "brother"));
                sx -= bw + gap;
            }
            if (t.Siblings.Count > 3) GUI.Label(new Rect(x0, me.y - 18f, Mathf.Max(80f, me.x - x0 - 2f * slot), 18f), "and " + (t.Siblings.Count - 3) + " more brothers and sisters", _pgSmall);   // (above their row, clear of the lines - it had sat on one)
            var partnerBox = new List<Rect>();
            float px = me.xMax + gap;
            for (int i = 0; i < t.Partners.Count; i++)
            {
                var pr = new Rect(px, me.y, bw, bh);
                if (pr.xMax > area.xMax) { partnerBox.Add(me); continue; }
                bool now = t.Partners[i] == t.Partner;
                KinBox(w, pr, t.Partners[i], now ? "partner" : (w.People.Exists(t.Partners[i]) && !w.People.Get(t.Partners[i]).Alive ? "partner, died" : "partner before"));
                partnerBox.Add(pr);
                px += bw + gap;
            }
            // the children, each group under the line from the person and that partner
            float ky = me.y + row; int k = 0;
            for (int g = 0; g < t.ChildrenOf.Count; g++)
            {
                var kids = t.ChildrenOf[g];
                if (kids.Count == 0) continue;
                Rect parentPair = g < partnerBox.Count ? Rect.MinMaxRect(Mathf.Min(me.x, partnerBox[g].x), me.y, Mathf.Max(me.xMax, partnerBox[g].xMax), me.yMax) : me;
                foreach (int c in kids)
                {
                    int col = k % perRow, rw = k / perRow;
                    var cr = new Rect(x0 + col * (bw + gap), ky + rw * row, bw, bh);
                    Line(new Rect(parentPair.center.x - 1f, parentPair.y, 2f, parentPair.height), cr, _dot);
                    KinBox(w, cr, c, w.People.Get(c).IsFemale ? "daughter" : "son");
                    k++;
                }
            }
            if (kidsTotal == 0) GUI.Label(new Rect(me.x - 60f, me.yMax + 10f, bw + 120f, 20f), "no children", _pgSmall);
            // grandchildren, named
            if (t.Grandchildren.Count > 0)
            {
                GUILayout.Label("Grandchildren (" + t.Grandchildren.Count + ")", _pgHead);
                GUILayout.BeginHorizontal();
                int n = 0;
                foreach (int g in t.Grandchildren)
                {
                    if (n > 0 && n % 6 == 0) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                    ref var q = ref w.People.Get(g);
                    if (GUILayout.Button(Given(w, g) + (q.Alive ? "" : " †"), q.Alive ? _pgBox : _pgBoxDead, GUILayout.Width(bw), GUILayout.Height(26f))) GoTo(g);
                    n++;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Each name opens that person's page; the dead are in faded ink. The colour at a name's edge is the cloth they wear, as the world draws them.", _pgSmall);
        }

        void WorkSection(TC2.Core.Simulation.World w, int pid, float cw)
        {
            ref var p = ref w.People.Get(pid);
            GUILayout.Label("Skills", _pgHead);
            GUILayout.Label("each grown by practice - and only by practice", _pgSmall);
            foreach (var sk in _pgSkills)
            {
                if (sk.Value < 0.02f && sk.Name != "Farming") continue;
                Bar(sk.Name, sk.Value, sk.Level + " - " + sk.Grown, sk.Name == "Fighting" ? new Color(0.62f, 0.12f, 0.10f) : new Color(0.55f, 0.40f, 0.12f), cw);
            }
            GUILayout.Label("Crafts they carry", _pgHead);
            GUILayout.Label("learnt from those who know them, or found by their own hands; a village keeps a craft while enough carry it", _pgSmall);
            bool any = false;
            for (int t = 0; t < TC2.Core.Knowledge.Tech.Count; t++)
            {
                float k = TC2.Core.Knowledge.Tech.Get(in p, t);
                if (k < 0.1f) continue;
                any = true;
                Bar(TC2.Core.Knowledge.Tech.Laws[t].Short, k, (k >= 0.5f ? "knows how to " : "learning to ") + TC2.Core.Knowledge.Tech.Laws[t].Name, new Color(0.30f, 0.42f, 0.55f), cw);
            }
            if (!any) GUILayout.Label("No craft beyond the everyday.", _pgBody);
            // what they are known for and the works they led (Biography's words)
            foreach (string line in _pgWorkText.Split('\n'))
                if (line.StartsWith("Known for: ", System.StringComparison.Ordinal) || line.StartsWith("Led the raising", System.StringComparison.Ordinal)) GUILayout.Label(line, _pgBody);
            if (_pgLife != null)
            {
                bool head = false;
                foreach (var e in _pgLife)
                {
                    if (e.Kind != Lives.Work) continue;
                    if (!head) { GUILayout.Label("Their works and finds", _pgHead); head = true; }
                    LifeRow(w, e, cw);
                }
            }
        }

        void TextSection(string text)
        {
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string t = raw.Trim();
                if (t.Length == 0) continue;
                int colon = t.IndexOf(": ", System.StringComparison.Ordinal);
                bool label = colon >= 2 && colon <= 24 && !char.IsDigit(t[0]);
                if (label) t = "<color=#8c2614>" + t.Substring(0, colon) + "</color>  " + t.Substring(colon + 2);
                GUILayout.Label(t, _pgBody);
            }
        }

        void HouseholdSection(TC2.Core.Simulation.World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            TextSection(_pgHome);
            if (p.Household == 0 || !w.Households.Exists(p.Household)) return;
            var mem = w.Members(p.Household);
            GUILayout.Label("Its people (" + mem.Count + ")", _pgHead);
            GUILayout.BeginHorizontal();
            int n = 0;
            for (int i = 0; i < mem.Count; i++)
            {
                if (n > 0 && n % 5 == 0) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                int m = mem[i];
                ref var q = ref w.People.Get(m);
                if (GUILayout.Button(Given(w, m) + "\n<size=11>" + ((int)q.AgeYears(w.Now)).ToString(CultureInfo.InvariantCulture) + (m == pid ? ", this page" : "") + "</size>", m == pid ? _pgBoxMe : _pgBox, GUILayout.Width(118f), GUILayout.Height(40f)) && m != pid) GoTo(m);
                n++;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            ref var hh = ref w.Households.Get(p.Household);
            GUILayout.BeginHorizontal();
            if (hh.Building != 0 && w.Buildings.Exists(hh.Building) && GUILayout.Button("their home  ›", _bkNav)) { _building = hh.Building; _personPage = false; var b = w.Buildings.Get(hh.Building); if (w.Settlements.Exists(b.Settlement)) { var at = w.Settlements.Get(b.Settlement).LocalToSphere(b.X, b.Y, w.Planet.Radius); Rig.FlyTo(at, System.Math.Min(Rig.Distance - w.Planet.Radius, 260.0)); } }
            if (p.Home != 0 && GUILayout.Button("their village  ›", _bkNav)) ShowPlace(p.Home);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>One entry of a life: its year in the margin with their age then, a mark in its kind's ink, its words sized by their weight - a click
        /// goes to the person it names or opens its record's causes.</summary>
        void LifeRow(TC2.Core.Simulation.World w, in Lives.Entry e, float cw)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=" + (e.Weight >= 2 ? 16 : 14) + ">" + SimTime.YearOf(e.Time) + "</size>\n<size=11><i>age " + e.Age + "</i></size>", _pgYear, GUILayout.Width(70));
            Rect mark = GUILayoutUtility.GetRect(14f, 18f, GUILayout.Width(14f));
            if (Event.current.type == EventType.Repaint)
            {
                float d = e.Weight >= 2 ? 9f : e.Weight == 1 ? 7f : 5f;
                GUI.color = KindInk(e.Kind); GUI.DrawTexture(new Rect(mark.x + (14f - d) * 0.5f, mark.y + 7f, d, d), _dot); GUI.color = Color.white;
            }
            var st = e.Weight >= 2 ? _pgEntryBig : e.Weight == 1 ? _pgEntry : _pgEntrySmall;
            string text = e.Text + (e.Person != 0 && e.Person != _selected && w.People.Exists(e.Person) ? "  <color=#8c2614><size=11>›</size></color>" : "");
            if (GUILayout.Button(text, st, GUILayout.MaxWidth(cw - 90f)))
            {
                if (e.Person != 0 && e.Person != _selected && w.People.Exists(e.Person)) GoTo(e.Person);
                else if (e.Event != 0) _pgWhy = _pgWhy == e.Event ? 0 : e.Event;
            }
            GUILayout.EndHorizontal();
            if (_pgWhy != 0 && _pgWhy == e.Event && w.History.TryGet(e.Event, out _))
            {
                int n = w.History.CausalChain(e.Event, _chain);
                for (int i = 1; i < n; i++)
                {
                    if (!w.History.TryGet(_chain[i], out var c)) { GUILayout.Label("      ← (a record forgotten)", _pgSmall); break; }
                    GUILayout.Label("      <color=#8c2614>←</color>  " + HistoryText.Dated(w, c), _pgSmall);
                }
                if (n <= 1) GUILayout.Label("      (no cause is recorded for it: a life's ordinary turn)", _pgSmall);
            }
        }

        void LifeSection(TC2.Core.Simulation.World w, int pid, float cw)
        {
            if (_pgLife == null || _pgLife.Count == 0) { GUILayout.Label("Nothing of their life is kept in the history yet.", _pgBody); return; }
            GUILayout.Label("A life as the world remembers it: their own records, their household's, their kin's, and what their village lived through while they lived in it. A name opens that person's page; a record opens its causes.", _pgSmall);
            GUILayout.Space(4);
            int lastDecade = int.MinValue;
            foreach (var e in _pgLife)
            {
                int dec = e.Age / 10;
                if (dec != lastDecade && e.Kind != Lives.Birth) { lastDecade = dec; GUILayout.Label(dec == 0 ? "Childhood" : dec == 1 ? "Youth" : "In their " + dec * 10 + "s", _pgHead); }
                else if (e.Kind == Lives.Birth) lastDecade = 0;
                LifeRow(w, e, cw);
            }
        }

        /// <summary>A small portrait from what the world draws them in: the cloth of their livery, a mantle and circlet for the first, a cap and shield for a
        /// sworn man, a child smaller, a woman's longer gown, the dead greyed. Cached per person (the page redraws it every frame).</summary>
        Texture2D Portrait(TC2.Core.Simulation.World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            Color cloth = WorldObjectsView.PageLivery(w, pid, out byte rank);
            int key = pid * 8 + rank * 2 + (p.Alive ? 1 : 0);
            if (_portraits.TryGetValue(key, out var tex) && tex != null) return tex;
            const int W = 156, H = 196;
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "tc2-portrait" };
            Color realm = rank != 0 && w.Settlements.Exists(p.Home) ? WorldObjectsView.PolityColour(w, p.Home) : cloth;
            var spec = new PortraitArt.Spec { Seed = pid, Age = (float)p.AgeYears(p.Alive ? w.Now : p.Died), Woman = p.IsFemale, Alive = p.Alive, Rank = rank, ClothR = cloth.r, ClothG = cloth.g, ClothB = cloth.b, RealmR = realm.r, RealmG = realm.g, RealmB = realm.b };
            var paint = new float[W * H * 4];
            PortraitArt.Paint(in spec, W, H, paint);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int o = ((H - 1 - y) * W + x) * 4;
                    px[y * W + x] = new Color32((byte)(paint[o] * 255f), (byte)(paint[o + 1] * 255f), (byte)(paint[o + 2] * 255f), (byte)(paint[o + 3] * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _portraits[key] = tex; _portraitOrder.Add(key);
            while (_portraitOrder.Count > 24) { int old = _portraitOrder[0]; _portraitOrder.RemoveAt(0); if (_portraits.TryGetValue(old, out var t0) && t0 != null) Destroy(t0); _portraits.Remove(old); }
            return tex;
        }
    }
}
