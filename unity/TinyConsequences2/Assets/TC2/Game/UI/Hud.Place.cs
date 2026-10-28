using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 29: a place read at a glance, and "why?") The place page's own sections beside the gazetteer's: Now - what its people are doing this moment
    /// as bars (in the fields, at the quarry, asleep...), its grain in days, what troubles it and what it is building, its last ten years from its own
    /// records and what drove them; Why? - the questions it can be asked, each bound to the record the world kept (why it pays its lord, walls itself, starved,
    /// why its first is first, why it was founded here), answered as a story from the root (Core/History/Whys); Realm - the realm it belongs to: its seat and
    /// ruler, its villages tier by tier, its people and sworn men, the tribute its seat took, its wars with outsiders and the bonds made and broken, and why it
    /// exists. Reads the world; writes nothing.
    /// </summary>
    public sealed partial class Hud
    {
        const int PlaceNowTab = 1, PlaceWhyTab = 2, PlaceRealmTab = 3, PlaceLandTab = 6;   // (Hud.PlaceTabs)
        readonly HashSet<long> _whyOpen = new HashSet<long>();
        // (built once a second: the counts of what its people do, the trend, the questions, the realm)
        int _plBuiltFor = -1; float _plBuiltAt = -99f;
        readonly List<KeyValuePair<string, int>> _plDoing = new List<KeyValuePair<string, int>>();
        List<string> _plTrend, _plNow; long _plWorst; List<Whys.Ask> _plAsks; Whys.RealmInfo _plRealm;

        void PlaceBuild(TC2.Core.Simulation.World w, int sid)
        {
            if (_plBuiltFor == sid && Time.unscaledTime - _plBuiltAt < 1f) return;
            if (_plBuiltFor != sid) _whyOpen.Clear();
            _plBuiltFor = sid; _plBuiltAt = Time.unscaledTime;
            var counts = new Dictionary<string, int>();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive) continue;
                string k = DoingWord(p.Act);
                counts.TryGetValue(k, out int n); counts[k] = n + 1;
            }
            _plDoing.Clear();
            foreach (var kv in counts) _plDoing.Add(kv);
            _plDoing.Sort((a, b) => { int c = b.Value.CompareTo(a.Value); return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key); });
            _plTrend = Whys.Trend(w, sid, 10, out _plWorst);
            _plNow = Whys.Now(w, sid);
            _plAsks = Whys.OfPlace(w, sid);
            _plRealm = Whys.Realm(w, sid);
        }

        /// <summary>What an activity is, in the few words a bar can carry.</summary>
        static string DoingWord(ActivityKind a)
        {
            switch (a)
            {
                case ActivityKind.Sleep: return "asleep";
                case ActivityKind.Infant: return "babies at home";
                case ActivityKind.Play: return "children at play";
                case ActivityKind.Eat: return "eating";
                case ActivityKind.Fields: return "in the fields";
                case ActivityKind.Wood: return "cutting wood";
                case ActivityKind.Water: return "fetching water";
                case ActivityKind.Ill: return "abed, sick or wounded";
                case ActivityKind.Watch: return "keeping watch";
                case ActivityKind.Craft: return "making and mending";
                case ActivityKind.Cook: return "grinding and cooking";
                case ActivityKind.Social: return "on the square";
                case ActivityKind.Idle: return "resting";
                case ActivityKind.TripOut: case ActivityKind.TripHome: case ActivityKind.Move: return "on the roads";
                case ActivityKind.Visit: return "visiting elsewhere";
                case ActivityKind.Flee: case ActivityKind.Wait: return "fleeing or waiting out danger";
                case ActivityKind.Hunt: return "hunting";
                case ActivityKind.Fish: return "fishing the shore";
                case ActivityKind.Gather: case ActivityKind.Forage: return "gathering wild food";
                case ActivityKind.Haul: return "carrying home";
                case ActivityKind.Build: return "building";
                case ActivityKind.Scout: return "looking over the land beyond";
                case ActivityKind.Tend: return "with the herds";
                case ActivityKind.Quarry: return "at the quarry";
                case ActivityKind.Smith: return "at the smithy";
                case ActivityKind.Train: return "at arms practice";
                case ActivityKind.Mine: return "at the mine";
                case ActivityKind.Burn: return "burning charcoal";
                case ActivityKind.Sail: return "out fishing in boats";
                case ActivityKind.Voyage: return "on voyages";
                case ActivityKind.Weave: return "at the weaver's shed";
                case ActivityKind.Pot: return "at the kiln";
                case ActivityKind.Garden: return "in the gardens";
                case ActivityKind.Thresh: return "threshing";
                case ActivityKind.Muck: return "carting muck to the fields";
                default: return "about their day";
            }
        }

        void PlaceNow(TC2.Core.Simulation.World w, int sid, float cw)
        {
            PersonStyles();
            PlaceBuild(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) { GUILayout.Label("Nobody lives here now: it was abandoned" + (s.Abandoned != 0 ? " in year " + SimTime.YearOf(s.Abandoned) : "") + ".", _pgBig); return; }
            GUILayout.Label("Its " + s.Population.ToString("N0", CultureInfo.InvariantCulture) + " people now", _pgHead);
            int max = 1; foreach (var kv in _plDoing) max = Mathf.Max(max, kv.Value);
            int shown = 0;
            foreach (var kv in _plDoing)
            {
                if (shown++ >= 12) break;
                Bar(Cap(kv.Key), kv.Value / (float)max, kv.Value.ToString(CultureInfo.InvariantCulture), new Color(0.45f, 0.36f, 0.22f), cw);
            }
            double days = s.Population > 0 ? s.Granary / s.Population : 0;
            GUILayout.Label("Its stores hold " + (days >= 1 ? days.ToString("0", CultureInfo.InvariantCulture) + " days of grain for each of its people" : "almost no grain") + (s.Crop != TC2.Core.Places.CropStage.Fallow ? "; its fields are " + CropWords(s.Crop) : "") + ".", _pgBody);
            GUILayout.Label("What it faces", _pgHead);
            if (_plNow.Count == 0) GUILayout.Label("Nothing troubles it now.", _pgBody);
            foreach (var l in _plNow) GUILayout.Label(l, _pgBody);
            GUILayout.Label("Its last ten years", _pgHead);
            foreach (var l in _plTrend) GUILayout.Label(l, _pgBody);
            if (_plWorst != 0 && w.History.TryGet(_plWorst, out _)) WhyToggle(w, "Why?", _plWorst, cw);
        }

        static string CropWords(TC2.Core.Places.CropStage c) => c == TC2.Core.Places.CropStage.Sown ? "sown" : c == TC2.Core.Places.CropStage.Growing ? "growing" : c == TC2.Core.Places.CropStage.Ripe ? "ripe" : c == TC2.Core.Places.CropStage.Harvested ? "reaped" : "bare";

        /// <summary>A question that opens its story beneath it.</summary>
        void WhyToggle(TC2.Core.Simulation.World w, string question, long ev, float cw)
        {
            bool open = _whyOpen.Contains(ev);
            if (GUILayout.Button((open ? "<color=#8c2614>–</color>  " : "<color=#8c2614>›</color>  ") + question, _pgEntryBig)) { if (open) _whyOpen.Remove(ev); else _whyOpen.Add(ev); }
            if (open) WhyStory(w, ev, cw);
        }

        /// <summary>A record's story from its root: each record dated, the link to the next in red, a second cause beside its step, where it began.</summary>
        void WhyStory(TC2.Core.Simulation.World w, long ev, float cw)
        {
            var story = Whys.Story(w, ev);
            foreach (var st in story)
            {
                string text = st.Text;
                int colon = text.IndexOf(": ", System.StringComparison.Ordinal);
                string date = colon > 0 && colon < 30 ? text.Substring(0, colon) : "", words = colon > 0 && colon < 30 ? text.Substring(colon + 2) : text;
                GUILayout.BeginHorizontal();
                GUILayout.Space(st.Side ? 46f : 18f);
                GUILayout.Label("<color=#8c2614><size=12>" + date.Replace("Year ", "") + "</size></color>", _pgAge, GUILayout.Width(96));
                GUILayout.Label(st.Side ? "<i>" + words + "</i>" : words, st.Side ? _pgEntrySmall : st.Event == ev ? _pgEntryBig : _pgEntry, GUILayout.MaxWidth(cw - 130f));
                GUILayout.EndHorizontal();
                if (st.Link.Length > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(st.Side ? 150f : 122f);
                    GUILayout.Label("<color=#8c2614><i>↓  " + st.Link + "</i></color>", _pgSmall);
                    GUILayout.EndHorizontal();
                }
            }
            string began = Whys.Began(w, story);
            if (began.Length > 0) { GUILayout.BeginHorizontal(); GUILayout.Space(122f); GUILayout.Label("<i>" + began + "</i>", _pgSmall); GUILayout.EndHorizontal(); }
            GUILayout.Space(6);
        }

        void PlaceWhy(TC2.Core.Simulation.World w, int sid, float cw)
        {
            PersonStyles();
            PlaceBuild(w, sid);
            GUILayout.Label("What the world remembers of why this place is as it is. Each answer is its record and the records it followed from, back to where it began.", _pgSmall);
            GUILayout.Space(4);
            if (_plAsks.Count == 0) GUILayout.Label("Nothing of why it is as it is is kept in the history now.", _pgBody);
            foreach (var q in _plAsks) WhyToggle(w, q.Question, q.Event, cw);
        }

        void PlaceRealm(TC2.Core.Simulation.World w, int sid, float cw)
        {
            PersonStyles();
            PlaceBuild(w, sid);
            var r = _plRealm;
            string n = w.History.NameOf(EntityRef.Settlement(sid));
            if (r.Villages.Count <= 1) { GUILayout.Label(n + " pays no lord and no village pays it: it stands alone.", _pgBody); return; }
            string seat = w.History.NameOf(EntityRef.Settlement(r.Seat));
            GUILayout.Label("The realm of " + seat, _pgBig);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ruled from " + seat + (r.Ruler != 0 && w.People.Exists(r.Ruler) ? " by" : ""), _pgBody, GUILayout.ExpandWidth(false));
            if (r.Ruler != 0 && w.People.Exists(r.Ruler) && GUILayout.Button(w.History.NameOf(EntityRef.Person(r.Ruler)) + "  ›", _bkNav)) ShowPerson(r.Ruler);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(r.Villages.Count + " villages, " + r.People.ToString("N0", CultureInfo.InvariantCulture) + " people; " + r.Sworn + " sworn men at their firsts' call; " +
                            (r.Tribute > 0 ? r.Tribute.ToString("N0", CultureInfo.InvariantCulture) + " days' grain paid to " + seat + " in the last year" : "no tribute reached " + seat + " in the last year") + ".", _pgBody);
            if (r.FirstBond != 0) WhyToggle(w, "Why does this realm exist?", r.FirstBond, cw);
            GUILayout.Label("Its villages, tier by tier", _pgHead);
            for (int i = 0; i < r.Villages.Count; i++)
            {
                int v = r.Villages[i];
                ref var vs = ref w.Settlements.Get(v);
                string line = w.History.NameOf(EntityRef.Settlement(v)) + "  <size=12><color=#6b5640>" + Gazetteer.Word(in vs).Substring(2) + " of " + vs.Population + (r.LordOf[i] != 0 && r.LordOf[i] != r.Seat ? ", pays " + w.History.NameOf(EntityRef.Settlement(r.LordOf[i])) : v == r.Seat ? ", the seat" : "") + "</color></size>";
                GUILayout.BeginHorizontal();
                GUILayout.Space(14f + 22f * r.Depth[i]);
                if (GUILayout.Button((v == sid ? "<b>" + line + "</b>" : line), _pgEntry) && v != sid) ShowPlace(v, PlaceRealmTab);
                GUILayout.EndHorizontal();
            }
            if (r.Wars.Count > 0)
            {
                GUILayout.Label("Its wars with outsiders, the last ten years", _pgHead);
                foreach (long e in r.Wars) if (w.History.TryGet(e, out var he)) WhyToggleDated(w, he, cw);
            }
            if (r.Bonds.Count > 0)
            {
                GUILayout.Label("Bonds made and broken among its villages", _pgHead);
                foreach (long e in r.Bonds) if (w.History.TryGet(e, out var he)) WhyToggleDated(w, he, cw);
            }
        }

        /// <summary>A record as a dated line that opens its story.</summary>
        void WhyToggleDated(TC2.Core.Simulation.World w, in HistoryEvent e, float cw)
        {
            bool open = _whyOpen.Contains(e.Id);
            if (GUILayout.Button("<color=#8c2614>" + SimTime.YearOf(e.Time) + "</color>  " + HistoryText.Describe(w, e), _pgEntry)) { if (open) _whyOpen.Remove(e.Id); else _whyOpen.Add(e.Id); }
            if (open) WhyStory(w, e.Id, cw);
        }
    }
}
