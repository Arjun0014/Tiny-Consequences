using System;
using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    /// <summary>
    /// (The memory pass, 14 §7) What a world's state holds, part by part, as its save writes it: `run --load F --statesizes` - the bytes of each store
    /// and how many rows it has, the parts that grow with a world's age (its record) set beside those that grow with its people. A save's bytes stand in
    /// for what the state holds in memory (the browser's heap held ~4x the headless managed heap, and grew with the world's age, not its people).
    /// </summary>
    static class StateSizes
    {
        static string F(double v, string f) => v.ToString(f, CultureInfo.InvariantCulture);

        public static void Print(World w)
        {
            var bw = new BinWriter(1 << 20);
            long all = 0;
            void Part(string name, int rows, Action<BinWriter> write)
            {
                bw.Clear();
                write(bw);
                all += bw.Length;
                Console.WriteLine("  " + name.PadRight(12) + F(bw.Length / 1048576.0, "0.00").PadLeft(9) + " MB" + (rows >= 0 ? "  " + rows.ToString("N0", CultureInfo.InvariantCulture).PadLeft(10) + " rows" + (rows > 0 ? ", " + F(bw.Length / (double)rows, "0") + " B a row" : "") : ""));
            }
            Console.WriteLine("statesizes at " + SimTime.Format(w.Now) + ": " + w.LivingPeople() + " living, " + w.InhabitedSettlements() + " places");
            Part("queue", -1, b => w.Queue.Write(b));
            Part("planet", -1, b => w.Planet.Write(b));
            Part("fire", -1, b => w.Fire.Write(b));
            Part("settlements", w.Settlements.Count, b => { for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) w.Settlements.Get(id).Write(b); });
            Part("households", w.Households.Count, b => { for (int id = w.Households.First(); id != 0; id = w.Households.Next(id)) w.Households.Get(id).Write(b); });
            Part("people", w.People.Count, b => { for (int id = w.People.First(); id != 0; id = w.People.Next(id)) w.People.Get(id).Write(b); });
            Part("herds", w.Herds.Count, b => { for (int id = w.Herds.First(); id != 0; id = w.Herds.Next(id)) w.Herds.Get(id).Write(b); });
            Part("beasts", w.Beasts.Count, b => { for (int id = w.Beasts.First(); id != 0; id = w.Beasts.Next(id)) w.Beasts.Get(id).Write(b); });
            Part("buildings", w.Buildings.Count, b => { for (int id = w.Buildings.First(); id != 0; id = w.Buildings.Next(id)) w.Buildings.Get(id).Write(b); });
            Part("deeds", w.Deeds.Count, b => { for (int id = w.Deeds.First(); id != 0; id = w.Deeds.Next(id)) w.Deeds.Get(id).Write(b); });
            Part("deedbook", -1, b => w.DeedBook.Write(b));
            Part("ways", -1, b => w.Ways.Write(b));
            Part("trade", -1, b => w.Trade.Write(b));
            Part("deposits", -1, b => w.Deposits.Write(b));
            Part("history", w.History.Count, b => w.History.Write(b));
            Part("commands", w.CommandLog.Count, b => { for (int i = 0; i < w.CommandLog.Count; i++) w.CommandLog[i].Write(b); });
            Part("metrics", -1, b => w.Metrics.Write(b));
            Part("annals", -1, b => w.Annals.Write(b));
            Part("holdings", -1, b => w.Holdings.Write(b));
            int alive = 0; for (int id = w.People.First(); id != 0; id = w.People.Next(id)) if (w.People.Get(id).Alive) alive++;
            Console.WriteLine("  all parts " + F(all / 1048576.0, "0.0") + " MB; people kept " + w.People.Count + " (living " + alive + ")");
        }
    }
}
