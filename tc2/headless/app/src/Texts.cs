using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// (session 32: readability) `texts --load SAVE [--out FILE] [--people N] [--places N]` - the words a player reads on the pages, as the HUD builds them
        /// from the Core's writers: for a sample of people (heads, firsts and lords, sworn men, the old, children, the dead, others) their card's line, page
        /// (Biography), life (Lives.Chronicle), following, temperament, skills and every Why? with its story; for a sample of places (towns, villages,
        /// hamlets, lords, tributaries, the abandoned) their page (Gazetteer), Now, last years, fields, realm and every Why? with its story. Reads only.
        /// </summary>
        public static int Texts(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            var sb = new StringBuilder();
            sb.Append("# texts of ").Append(a.Get("load")).Append(" at ").Append(SimTime.Format(w.Now)).Append('\n');
            int nPeople = a.Int("people", 40), nPlaces = a.Int("places", 15);
            foreach (var (why, pid) in SamplePeople(w, nPeople)) PersonTexts(w, pid, why, sb);
            foreach (var (why, sid) in SamplePlaces(w, nPlaces)) PlaceTexts(w, sid, why, sb);
            if (a.Has("out")) File.WriteAllText(a.Get("out"), sb.ToString()); else Console.Write(sb.ToString());
            return 0;
        }

        static List<(string, int)> SamplePeople(World w, int n)
        {
            var heads = new List<int>(); var firsts = new List<int>(); var lords = new List<int>(); var sworn = new List<int>(); var old = new List<int>();
            var kids = new List<int>(); var dead = new List<int>(); var rest = new List<int>();
            var seats = new HashSet<int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) { ref var s = ref w.Settlements.Get(sid); if (s.Inhabited && s.Lord != 0) seats.Add(s.Lord); }
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || s.Head == 0 || !w.People.Exists(s.Head)) continue;
                (seats.Contains(sid) ? lords : firsts).Add(s.Head);
            }
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                double age = p.AgeYears(w.Now);
                if (!p.Alive) dead.Add(id);
                else if (p.Sworn != 0) sworn.Add(id);
                else if (age >= 60) old.Add(id);
                else if (age < 12) kids.Add(id);
                else if (age >= 18) heads.Add(id);
                else rest.Add(id);
            }
            var l = new List<(string, int)>();
            void Take(string why, List<int> from, int k)
            {
                if (from.Count == 0) return;
                for (int i = 0; i < k && i < from.Count; i++) { int pid = from[(int)((long)i * from.Count / Math.Min(k, from.Count))]; if (!l.Exists(x => x.Item2 == pid)) l.Add((why, pid)); }
            }
            int u = Math.Max(1, n / 10);
            Take("a lord's first", lords, u + 1); Take("a village's first", firsts, u + 1); Take("sworn", sworn, u); Take("a grown man or woman", heads, u * 2);
            Take("old", old, u); Take("a child", kids, u); Take("dead", dead, u * 2 + 1); Take("other", rest, u);
            return l;
        }

        static List<(string, int)> SamplePlaces(World w, int n)
        {
            var l = new List<(string, int)>();
            var towns = new List<int>(); var villages = new List<int>(); var hamlets = new List<int>(); var gone = new List<int>(); var lords = new List<int>(); var tribs = new List<int>();
            var seats = new HashSet<int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) { ref var s = ref w.Settlements.Get(sid); if (s.Inhabited && s.Lord != 0) seats.Add(s.Lord); }
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) gone.Add(sid);
                else if (seats.Contains(sid)) lords.Add(sid);
                else if (s.Lord != 0) tribs.Add(sid);
                else if (s.Population >= 300) towns.Add(sid);
                else if (s.Population >= 50) villages.Add(sid);
                else hamlets.Add(sid);
            }
            void Take(string why, List<int> from, int k) { for (int i = 0; i < k && i < from.Count; i++) l.Add((why, from[(int)((long)i * from.Count / Math.Min(k, from.Count))])); }
            int u = Math.Max(1, n / 6);
            Take("a lord", lords, u + 1); Take("a tributary", tribs, u); Take("a town", towns, u); Take("a village", villages, u); Take("a hamlet", hamlets, u); Take("abandoned", gone, u);
            return l;
        }

        static void Story(World w, long ev, StringBuilder sb)
        {
            var st = Whys.Story(w, ev);
            foreach (var s in st) sb.Append("      ").Append(s.Side ? "  (beside) " : "").Append(s.Text).Append(s.Link.Length > 0 ? "  -- " + s.Link : "").Append('\n');
            string began = Whys.Began(w, st);
            if (began.Length > 0) sb.Append("      => ").Append(began).Append('\n');
        }

        static void PersonTexts(World w, int pid, string why, StringBuilder sb)
        {
            ref var p = ref w.People.Get(pid);
            sb.Append("\n\n######## PERSON #").Append(pid).Append(" (").Append(why).Append(")\n");
            if (p.Alive) sb.Append("-- card: ").Append(HistoryText.Doing(w, in p)).Append('\n');
            sb.Append("-- title: ").Append(Biography.Title(w, pid)).Append("\n-- standing: ").Append(Biography.Standing(w, pid)).Append('\n');
            sb.Append(Biography.All(w, pid));
            sb.Append("-- life (the page's Life)\n");
            foreach (var e in TC2.Core.History.Lives.Chronicle(w, pid)) sb.Append("   [").Append(e.Age).Append("] ").Append(e.Text).Append('\n');
            var f = TC2.Core.History.Lives.Following(w, pid);
            if (f.Count > 0) { sb.Append("-- following\n"); foreach (var s in f) sb.Append("   ").Append(s).Append('\n'); }
            foreach (var t in TC2.Core.History.Lives.Temperament(in p)) sb.Append("-- temperament: ").Append(t[0]).Append(" - ").Append(t[1]).Append('\n');
            var sk = new StringBuilder();
            foreach (var s in TC2.Core.History.Lives.Skills(in p)) if (s.Value >= 0.1f) sk.Append(s.Name).Append(' ').Append(s.Level).Append("; ");
            if (sk.Length > 0) sb.Append("-- skills: ").Append(sk).Append('\n');
            var kin = TC2.Core.History.Lives.KinOf(w, pid);
            sb.Append("-- kin: ").Append(kin.Partners.Count).Append(" partners, ").Append(kin.Siblings.Count).Append(" siblings, ").Append(kin.Grandchildren.Count).Append(" grandchildren\n");
            foreach (var q in Whys.OfPerson(w, pid)) { sb.Append("-- WHY: ").Append(q.Question).Append('\n'); Story(w, q.Event, sb); }
        }

        static void PlaceTexts(World w, int sid, string why, StringBuilder sb)
        {
            sb.Append("\n\n######## PLACE #").Append(sid).Append(" (").Append(why).Append(")\n");
            sb.Append(Gazetteer.All(w, sid));
            sb.Append("-- now\n"); foreach (var s in Whys.Now(w, sid)) sb.Append("   ").Append(s).Append('\n');
            sb.Append("-- the last ten years\n"); foreach (var s in Whys.Trend(w, sid, 10, out long worst)) sb.Append("   ").Append(s).Append('\n');
            sb.Append("-- fields\n"); foreach (var s in Whys.Fields(w, sid)) sb.Append("   ").Append(s).Append('\n');
            var r = Whys.Realm(w, sid);
            if (r.Villages.Count > 1)
            {
                sb.Append("-- realm: seat ").Append(w.History.NameOf(EntityRef.Settlement(r.Seat))).Append(", ruler ").Append(r.Ruler != 0 ? w.History.NameOf(EntityRef.Person(r.Ruler)) : "-")
                  .Append(", ").Append(r.Villages.Count).Append(" villages, ").Append(r.People).Append(" people, ").Append(r.Sworn).Append(" sworn, tribute ").Append(r.Tribute.ToString("0.0")).Append('\n');
                foreach (long e in r.Wars) if (w.History.TryGet(e, out var he)) sb.Append("   war: ").Append(HistoryText.Dated(w, he)).Append('\n');
                foreach (long e in r.Bonds) if (w.History.TryGet(e, out var he)) sb.Append("   bond: ").Append(HistoryText.Dated(w, he)).Append('\n');
            }
            foreach (var q in Whys.OfPlace(w, sid)) { sb.Append("-- WHY: ").Append(q.Question).Append('\n'); Story(w, q.Event, sb); }
        }
    }
}
