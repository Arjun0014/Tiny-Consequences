using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.History
{
    /// <summary>
    /// (session 29: the long skip as a timelapse) What a span watched from orbit tells as it passes and when it ends. As the years run, only the great
    /// records come up as headlines - a new sickness and each plague's first coming, a famine, a village founded or abandoned or split, a realm grown or
    /// broken, the first raid between two villages in the span, a succession at a realm's seat, a discovery, walls of stone, the earth's and the sea's
    /// violence, and whatever the player's own hand set going - each once (a plague by its sickness, a war by its pair, a famine by its village and year),
    /// so a century reads as a dozen lines, not a feed. When the span ends, <see cref="Summary"/> says what the years did from the world's own counters
    /// and state: years, people, births and deaths by cause, places founded and lost, realms risen, grown and fallen, plagues, wars, and what came of the
    /// player's hand. Every word is a record or a count; nothing is guessed. An observer: it writes nothing in the world.
    /// </summary>
    public sealed class Headlines
    {
        public struct Item
        {
            public long Event, Time;
            public string Kicker, Text;
            public byte Rank;          // 1 notable, 2 great, 3 the player's hand or the world's greatest
            public int Settlement;
        }

        // what the span began with
        public readonly long FromTime, FromHistory;
        public readonly int FromPeople, FromPlaces;
        readonly long _births, _deaths, _hunger, _sick, _wounds, _disaster, _raids, _famines, _foundings, _epidemics;
        readonly Dictionary<int, int> _realms = new Dictionary<int, int>();   // seat -> villages (with the seat) at the start
        long _cursor;
        // told once
        readonly HashSet<long> _pathogens = new HashSet<long>();
        readonly HashSet<long> _pairs = new HashSet<long>();
        readonly HashSet<long> _famine = new HashSet<long>();
        // what the span's records hold beyond the counters
        readonly List<int> _founded = new List<int>(), _abandoned = new List<int>();
        readonly List<long> _plagues = new List<long>();
        int _yours, _wars, _splits, _discoveries;
        long _firstYours;

        public Headlines(World w)
        {
            FromTime = w.Now; FromHistory = w.History.LastId; _cursor = FromHistory;
            FromPeople = w.LivingPeople(); FromPlaces = w.InhabitedSettlements();
            var m = w.Metrics;
            _births = m.Births; _deaths = m.Deaths; _hunger = m.HungerDeaths; _sick = m.DiseaseDeaths; _wounds = m.WoundDeaths;
            _disaster = m.EruptionDeaths + m.QuakeDeaths + m.TsunamiDeaths + m.StormDeaths; _raids = m.Raids; _famines = m.Famines; _foundings = m.Foundings; _epidemics = m.Epidemics;
            Realms(w, _realms);
        }

        /// <summary>The realms standing now: each seat (a village that is paid and pays nobody) with its villages, the seat counted.</summary>
        public static void Realms(World w, Dictionary<int, int> into)
        {
            into.Clear();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || s.Lord != 0) continue;
                int n = w.Conflict.PolitySize(sid);
                if (n > 0) into[sid] = n + 1;
            }
        }

        static string Name(World w, int sid) => w.History.NameOf(EntityRef.Settlement(sid));

        /// <summary>The records made since the last call that are headlines, in time order, into <paramref name="into"/>; returns how many.</summary>
        public int Poll(World w, List<Item> into)
        {
            var H = w.History;
            int n = 0;
            for (int i = H.FirstIndexAfter(_cursor); i < H.Count; i++)
            {
                var e = H.At(i);
                Count(w, in e);
                if (Pick(w, in e, out var it)) { into.Add(it); n++; }
            }
            _cursor = H.LastId;
            return n;
        }

        void Count(World w, in HistoryEvent e)
        {
            switch (e.Kind)
            {
                case HistoryKind.SettlementFounded: if (e.Subject.Kind == EntityKind.Settlement) _founded.Add(e.Subject.Id); break;
                case HistoryKind.SettlementAbandoned: if (e.Subject.Kind == EntityKind.Settlement) _abandoned.Add(e.Subject.Id); break;
                case HistoryKind.VillageSplit: _splits++; break;
                case HistoryKind.TechniqueFound: _discoveries++; break;
            }
            if (e.RootKind == RootKind.You && e.Kind != HistoryKind.GodCommand) { _yours++; if (_firstYours == 0) _firstYours = e.Id; }
        }

        /// <summary>Whether a record is a headline of the span, and its kicker and words (the ledger's own sentence).</summary>
        bool Pick(World w, in HistoryEvent e, out Item it)
        {
            it = default;
            string kicker = null; byte rank = 0;
            switch (e.Kind)
            {
                case HistoryKind.PathogenAppeared: kicker = "A new sickness"; rank = 2; _pathogens.Add(e.Subject.Packed); _plagues.Add(e.Subject.Packed); break;
                case HistoryKind.EpidemicArrived:
                    if (_pathogens.Add(e.Object.Packed)) { kicker = "Plague"; rank = 1; _plagues.Add(e.Object.Packed); }
                    break;
                case HistoryKind.EpidemicEnded: if (e.Magnitude >= 60f) { kicker = "Plague"; rank = 2; } break;
                case HistoryKind.Famine:
                    {
                        long key = (long)e.Settlement * 100000L + SimTime.YearOf(e.Time);
                        if (_famine.Add(key)) { kicker = "Famine"; rank = 1; }
                        break;
                    }
                case HistoryKind.SettlementFounded: kicker = "Founded"; rank = 1; break;
                case HistoryKind.SettlementAbandoned: kicker = "Abandoned"; rank = 2; break;
                case HistoryKind.VillageSplit: kicker = "A village splits"; rank = 1; break;
                case HistoryKind.TributeAgreed: if (e.ArgA >= 3) { kicker = "A realm grows"; rank = (byte)(e.ArgA >= 8 ? 2 : 1); } break;
                case HistoryKind.TributeEnded: if (e.ArgA != 2) { kicker = "A bond broken"; rank = 1; } break;
                case HistoryKind.Raid:
                    {
                        int a = e.Subject.Kind == EntityKind.Settlement ? e.Subject.Id : 0, b = e.Object.Kind == EntityKind.Settlement ? e.Object.Id : 0;
                        long key = a < b ? (long)a * 1000003L + b : (long)b * 1000003L + a;
                        if (a != 0 && b != 0 && _pairs.Add(key)) { _wars++; kicker = "War"; rank = 1; }
                        break;
                    }
                case HistoryKind.HeadmanRose:
                    // a succession at a realm's seat (the place others pay)
                    if (e.Settlement != 0 && w.Conflict.PolitySize(e.Settlement) >= 2) { kicker = "Succession"; rank = 1; }
                    break;
                case HistoryKind.TechniqueFound: kicker = "Discovery"; rank = 2; break;
                case HistoryKind.WallBuilt: if (e.ArgB == 2) { kicker = "Walls of stone"; rank = 1; } break;
                case HistoryKind.Eruption: if (e.Magnitude >= 2f) { kicker = "Eruption"; rank = (byte)(e.Magnitude >= 3f ? 2 : 1); } break;
                case HistoryKind.Quake: if (e.Magnitude >= 5.5f) { kicker = "Earthquake"; rank = (byte)(e.Magnitude >= 6.5f ? 2 : 1); } break;
                case HistoryKind.Tsunami: kicker = "A great wave"; rank = 2; break;
                case HistoryKind.StormLandfall: if (e.Importance >= 20f) { kicker = "A great storm"; rank = 1; } break;
                case HistoryKind.MeteorStruck: kicker = "A falling star"; rank = 2; break;
                case HistoryKind.IceAge: case HistoryKind.IceAgeEnded: kicker = "The climate"; rank = 2; break;
                case HistoryKind.HomesDrowned: kicker = "The sea"; rank = 1; break;
                case HistoryKind.HomesShaken: kicker = "The quake"; rank = 1; break;
                case HistoryKind.Extinction: kicker = "Gone from the world"; rank = 1; break;
            }
            // what the player's hand set going: its great consequences come up as theirs, whatever their kind
            if (e.RootKind == RootKind.You && e.Kind != HistoryKind.GodCommand && (rank > 0 || e.Tier >= Tier.Historical))
            {
                kicker = "Your hand"; rank = 3;
            }
            if (kicker == null || rank == 0) return false;
            it = new Item { Event = e.Id, Time = e.Time, Kicker = kicker, Rank = rank, Settlement = e.Settlement, Text = HistoryText.Describe(w, e) };
            return true;
        }

        static string N(long v) => v.ToString("N0", CultureInfo.InvariantCulture);

        static string Count(long n, string one, string many) => N(n) + " " + (n == 1 ? one : many);

        /// <summary>
        /// What the span did, a line a theme: the years; the people then and now with the births and the deaths by their causes; the places founded and lost;
        /// the realms that rose, grew, shrank and fell (from the bonds standing at either end); plagues, wars and famines; discoveries; what came of the
        /// player's hand. From the world's own counters (Metrics), its state and the span's records - nothing estimated.
        /// </summary>
        public List<string> Summary(World w)
        {
            var lines = new List<string>();
            var m = w.Metrics;
            long years = SimTime.YearOf(w.Now) - SimTime.YearOf(FromTime);
            long days = (w.Now - FromTime) / SimTime.Day;
            lines.Add(years >= 1 ? Count(years, "year", "years") + " passed: year " + SimTime.YearOf(FromTime) + " to year " + SimTime.YearOf(w.Now) + "."
                                 : Count(days, "day", "days") + " passed.");
            int people = w.LivingPeople();
            long births = m.Births - _births, deaths = m.Deaths - _deaths, hunger = m.HungerDeaths - _hunger, sick = m.DiseaseDeaths - _sick, wounds = m.WoundDeaths - _wounds;
            long disaster = m.EruptionDeaths + m.QuakeDeaths + m.TsunamiDeaths + m.StormDeaths - _disaster;
            string change = FromPeople > 0 ? " (" + ((people - FromPeople) * 100.0 / FromPeople).ToString("+0;-0", CultureInfo.InvariantCulture) + " %)" : "";
            lines.Add("People: " + N(FromPeople) + " then, " + N(people) + " now" + change + " - " + Count(births, "birth", "births") + ", " + Count(deaths, "death", "deaths") + ".");
            var causes = new List<string>();
            if (hunger > 0) causes.Add(N(hunger) + " of hunger");
            if (sick > 0) causes.Add(N(sick) + " of sickness");
            if (wounds > 0) causes.Add(N(wounds) + " of wounds");
            if (disaster > 0) causes.Add(N(disaster) + " in the earth's and the sky's violence");
            if (causes.Count > 0) lines.Add("Of the dead, " + string.Join(", ", causes) + "; the rest of their years.");
            int places = w.InhabitedSettlements();
            string placeLine = "Places: " + N(FromPlaces) + " then, " + N(places) + " now";
            if (_founded.Count > 0) placeLine += "; founded " + Names(w, _founded, 4);
            if (_abandoned.Count > 0) placeLine += "; abandoned " + Names(w, _abandoned, 4);
            lines.Add(placeLine + ".");
            // the realms at either end
            var now = new Dictionary<int, int>();
            Realms(w, now);
            var said = new List<string>();
            var keys = new List<int>(now.Keys); keys.Sort();
            foreach (int seat in keys)
            {
                int n = now[seat];
                if (!_realms.TryGetValue(seat, out int was)) { if (n >= 3) said.Add(Name(w, seat) + "'s realm rose (" + n + " villages)"); }
                else if (n >= was + 2) said.Add(Name(w, seat) + "'s grew from " + was + " to " + n + " villages");
                else if (n <= was - 2) said.Add(Name(w, seat) + "'s shrank from " + was + " to " + n + " villages");
            }
            var gone = new List<int>(_realms.Keys); gone.Sort();
            foreach (int seat in gone) if (_realms[seat] >= 3 && !now.ContainsKey(seat)) said.Add(Name(w, seat) + "'s realm of " + _realms[seat] + " villages fell apart");
            if (said.Count > 0) lines.Add("Realms: " + Join(said, 4) + ".");
            else if (now.Count > 0) lines.Add("Realms: " + Count(now.Count, "realm stands", "realms stand") + " as before.");
            var troubles = new List<string>();
            long famines = m.Famines - _famines, raids = m.Raids - _raids;
            if (_plagues.Count > 0) troubles.Add(Count(_plagues.Count, "plague", "plagues") + " (" + Join(PlagueNames(w), 3) + ")");
            if (famines > 0) troubles.Add(Count(famines, "famine", "famines"));
            if (raids > 0) troubles.Add(Count(raids, "raid", "raids") + (_wars > 0 ? " between " + Count(_wars, "pair", "pairs") + " of villages" : ""));
            if (_splits > 0) troubles.Add(Count(_splits, "village split", "villages split") + " over a succession");
            if (troubles.Count > 0) lines.Add("Troubles: " + string.Join("; ", troubles) + ".");
            if (_discoveries > 0) lines.Add("Discoveries: " + Count(_discoveries, "craft", "crafts") + " found where nobody had known them.");
            if (_yours > 0) lines.Add("Your hand: " + Count(_yours, "record", "records") + " of these years trace back to what you did.");
            return lines;
        }

        List<string> PlagueNames(World w)
        {
            var l = new List<string>();
            foreach (long p in _plagues) l.Add(w.History.NameOf(EntityRef.FromPacked(p)));
            return l;
        }

        static string Names(World w, List<int> sids, int max)
        {
            var l = new List<string>();
            for (int i = 0; i < sids.Count; i++) l.Add(Name(w, sids[i]));
            return Join(l, max);
        }

        static string Join(List<string> l, int max)
        {
            if (l.Count == 0) return "";
            if (l.Count <= max) return l.Count == 1 ? l[0] : string.Join(", ", l.GetRange(0, l.Count - 1)) + " and " + l[l.Count - 1];
            return string.Join(", ", l.GetRange(0, max)) + " and " + (l.Count - max) + " more";
        }
    }
}
