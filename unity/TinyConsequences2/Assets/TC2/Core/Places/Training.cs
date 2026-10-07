using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Fighters made by practice (C1.5, D2-094; 23 §1, §4): a village that fears its neighbours (`Forge.Threatened`) lays out a training
    /// ground beyond its homes, and its fit, bold people of 16-45 practise there - a two-hour session every third day, in the afternoon -
    /// growing a **fighting skill** (`Person.SkFight`) toward a ceiling, faster with a skilled one among them (the village's best: a trainer).
    /// A fight survived teaches more than months of practice (`Veteran`: every blow given or taken in a raid or at a store). Skill fades
    /// without practice and with age, and dies with the person: a village whose seasoned fighters fell is weaker for a generation. Trained
    /// fighters strike harder (`Conflict.Strength` x (1 + 0.6 skill)). The hours are real: off the fields, the woods and the wild.
    /// </summary>
    public static class Training
    {
        /// <summary>Skill a practice hour gives a beginner (less as they near the ceiling; more with a trainer).</summary>
        public const double PerHour = 0.004;
        /// <summary>How much the village's best fighter lifts the others' learning (x (1 + Lift x best)).</summary>
        public const double Lift = 0.5;
        /// <summary>Share of skill lost in a year without practice (and twice that past fifty).</summary>
        public const double Fade = 0.05;
        /// <summary>Hours of a session.</summary>
        public const double Session = 2.0;
        /// <summary>What a blow given or taken teaches (of the skill still to learn).</summary>
        public const double FightLesson = 0.04;
        /// <summary>Boldness (0-255) from which a person chooses to practise when the village fears.</summary>
        public const byte BoldFrom = 90;

        /// <summary>The village's working training ground (0 none).</summary>
        public static int Ground(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Ground != 0 && w.Buildings.Exists(s.Ground) && Structures.Working(in w.Buildings.Get(s.Ground), w.Now)) return s.Ground;
            return 0;
        }

        /// <summary>One who would practise: 16-45, fit, bold enough, not near a birth nor nursing a young child, not hurt.</summary>
        public static bool Willing(in Person p, long now)
        {
            double age = p.AgeYears(now);
            if (age < 16 || age >= 46 || p.Health < 0.7f || p.Bold < BoldFrom || p.Hurt > 0.1f) return false;
            if (p.PregDue != 0 && p.PregDue - now < 20 * SimTime.Day) return false;
            if (p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2) return false;
            return true;
        }

        /// <summary>A person's practice days: one in three, by their own turn (the village's people do not all go the same day).</summary>
        public static bool Day(int pid, long localDay) => SimTime.FloorMod(localDay + pid, 3) == 0;

        /// <summary>Practice days a step of days from this local day holds for a person (DEEP).</summary>
        public static int Days(int pid, long localDay, int days)
        {
            int n = 0;
            for (int k = 0; k < days; k++) if (Day(pid, localDay + k)) n++;
            return n;
        }

        /// <summary>The village practises now: it fears its neighbours and its ground stands.</summary>
        public static bool Practising(World w, in Settlement s, out int ground)
        {
            ground = 0;
            if (!Forge.Threatened(in s)) return false;
            ground = Ground(w, s.Id);
            return ground != 0;
        }

        /// <summary>Hours of practice: skill toward the ceiling, lifted by the village's best; the ground's use.</summary>
        public static void Practise(World w, ref Person p, int sid, int ground, double hours)
        {
            if (hours <= 0) return;
            ref var s = ref w.Settlements.Get(sid);
            double gain = PerHour * hours * (1.0 + Lift * (double)s.BestFight);
            p.SkFight = (float)DMath.Min(1.0, (double)p.SkFight + (1.0 - (double)p.SkFight) * DMath.Min(1.0, gain));
            ref var b = ref w.Buildings.Get(ground);
            b.Use = (float)(b.Use + hours);
            w.Metrics.TrainHours += hours;
        }

        /// <summary>A blow given or taken in a fight (a raid, a taker at a store): the lesson of it.</summary>
        public static void Veteran(ref Person p)
        {
            p.SkFight = (float)DMath.Min(1.0, (double)p.SkFight + (1.0 - (double)p.SkFight) * FightLesson);
            if (p.Fights < 255) p.Fights++;
        }

        /// <summary>
        /// The village's day (both resolutions): its people's skill fades without practice (twice as fast past fifty), and its best fighter
        /// is known (the one the others learn from).
        /// </summary>
        public static void Daily(World w, int sid, int days)
        {
            ref var s = ref w.Settlements.Get(sid);
            var res = w.Residents(sid);
            long now = w.Now;
            double keep = SettlementSystem.Keep(Fade / SimTime.DaysPerYear, days), keepOld = SettlementSystem.Keep(2.0 * Fade / SimTime.DaysPerYear, days);
            double best = 0;
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive || p.SkFight <= 0f) continue;
                p.SkFight = (float)((double)p.SkFight * (p.AgeYears(now) > 50 ? keepOld : keep));
                if (p.SkFight < 0.005f) p.SkFight = 0f;
                if (p.SkFight > best) best = p.SkFight;
            }
            ref var s2 = ref w.Settlements.Get(sid);
            s2.BestFight = (float)best;
        }
    }
}
