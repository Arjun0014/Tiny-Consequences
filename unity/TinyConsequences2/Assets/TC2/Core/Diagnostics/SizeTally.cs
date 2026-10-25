using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Core.Diagnostics
{
    /// <summary>
    /// Tools only (villages deserted, measured first): how villages of each size live - a village-year counted at each village's yearly weighing by its
    /// people then (its ration, its land against its people's need, its store), and births, deaths by cause, households moving out and in, parties
    /// setting out, famines and abandonment as they happen, each by its village's size at that moment. Off (null) unless a tool turns it on; never
    /// saved or hashed, never read by the simulation.
    /// </summary>
    public static class SizeTally
    {
        public static readonly int[] Bounds = { 25, 50, 100, 200, 400 };   // classes: under 25, 25-49, 50-99, 100-199, 200-399, 400 and more
        public static readonly string[] Names = { "under 25", "25-49", "50-99", "100-199", "200-399", "400+" };
        public const int Classes = 6;

        // per class: village-years, person-years, ration sum, land/need sum, store-years sum, short years (ration under 0.9), births, deaths, deaths by
        // hunger, sickness, wounds (fights and raids), childbirth, the old; households moved out, in; parties set out, their households; famines; abandoned
        public const int VY = 0, PY = 1, Ration = 2, Land = 3, Store = 4, Short = 5, Births = 6, Deaths = 7, Hunger = 8, Sick = 9, Wounds = 10, Childbirth = 11,
            OtherDeaths = 12, MovedOut = 13, MovedIn = 14, Parties = 15, PartyHh = 16, Famines = 17, Abandoned = 18, Stride = 19;
        public static double[] Sums;

        public static int ClassOf(int people)
        {
            int k = 0;
            while (k < Bounds.Length && people >= Bounds[k]) k++;
            return k;
        }

        static int ClassOfVillage(World w, int sid) => sid > 0 && w.Settlements.Exists(sid) ? ClassOf(w.Settlements.Get(sid).Population) : -1;

        /// <summary>A village's year, at its weighing (SettlementSystem, the decide day).</summary>
        public static void Year(World w, int sid)
        {
            var t = Sums;
            if (t == null || !w.Settlements.Exists(sid)) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            int o = ClassOf(s.Population) * Stride;
            t[o + VY]++; t[o + PY] += s.Population; t[o + Ration] += s.Ration;
            double need = s.Population * Places.SettlementSystem.HaPerHead(w, in s);
            t[o + Land] += need > 0 ? s.ArableHa / need : 1.0;
            t[o + Store] += s.Population > 0 ? s.Granary / (s.Population * w.Config.FoodPerAdultDay * SimTime.DaysPerYear) : 0.0;
            if (s.Ration < 0.9) t[o + Short]++;
        }

        /// <summary>A recorded event (World.Record), by its village's size now.</summary>
        public static void Event(World w, in HistoryEvent e)
        {
            var t = Sums;
            if (t == null) return;
            switch (e.Kind)
            {
                case HistoryKind.PersonBorn: { int k = ClassOfVillage(w, e.Settlement); if (k >= 0) t[k * Stride + Births]++; break; }
                case HistoryKind.PersonDied:
                    {
                        int k = ClassOfVillage(w, e.Settlement); if (k < 0) break;
                        int o = k * Stride;
                        t[o + Deaths]++;
                        if (e.ArgA == 2) t[o + Hunger]++; else if (e.ArgA == 10) t[o + Sick]++; else if (e.ArgA == 11 || e.ArgA == 13) t[o + Wounds]++; else if (e.ArgA == 3) t[o + Childbirth]++; else t[o + OtherDeaths]++;
                        break;
                    }
                case HistoryKind.HouseholdMoved:
                    {
                        int k = ClassOfVillage(w, e.Settlement); if (k >= 0) t[k * Stride + MovedOut] += e.ArgA;
                        if (e.Object.Kind == EntityKind.Settlement) { int d = ClassOfVillage(w, e.Object.Id); if (d >= 0) t[d * Stride + MovedIn] += e.ArgA; }
                        break;
                    }
                case HistoryKind.PartySetOut: { int k = ClassOfVillage(w, e.Settlement); if (k >= 0) { t[k * Stride + Parties]++; t[k * Stride + PartyHh] += e.ArgA; } break; }
                case HistoryKind.Famine: { int k = ClassOfVillage(w, e.Settlement); if (k >= 0) t[k * Stride + Famines]++; break; }
                case HistoryKind.SettlementAbandoned: { if (e.Settlement > 0) t[0 * Stride + Abandoned]++; break; }   // (it had none left: counted under the smallest)
            }
        }
    }
}
