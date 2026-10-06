using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.History
{
    /// <summary>One year of the world in numbers (D2-077; 11 §8): what eras are read from after the fact.</summary>
    public struct AnnalYear
    {
        public int Year;
        public int People, Places;
        public int Births, Deaths, Hunger, Sick, Wounds, Disaster;
        public int Famines, Epidemics, Raids, Walls, Tributes, Founded, Abandoned, Found, Lost;
        public float Temp;

        public void Write(BinWriter w)
        {
            w.Int(Year); w.Int(People); w.Int(Places); w.Int(Births); w.Int(Deaths); w.Int(Hunger); w.Int(Sick); w.Int(Wounds); w.Int(Disaster);
            w.Int(Famines); w.Int(Epidemics); w.Int(Raids); w.Int(Walls); w.Int(Tributes); w.Int(Founded); w.Int(Abandoned); w.Int(Found); w.Int(Lost); w.Float(Temp);
        }

        public static AnnalYear Read(BinReader r) => new AnnalYear
        {
            Year = r.Int(), People = r.Int(), Places = r.Int(), Births = r.Int(), Deaths = r.Int(), Hunger = r.Int(), Sick = r.Int(), Wounds = r.Int(), Disaster = r.Int(),
            Famines = r.Int(), Epidemics = r.Int(), Raids = r.Int(), Walls = r.Int(), Tributes = r.Int(), Founded = r.Int(), Abandoned = r.Int(), Found = r.Int(), Lost = r.Int(), Temp = r.Float(),
        };

        public void Hash(ref StateHasher h)
        {
            h.Add(Year); h.Add(People); h.Add(Places); h.Add(Births); h.Add(Deaths); h.Add(Hunger); h.Add(Sick); h.Add(Wounds); h.Add(Disaster);
            h.Add(Famines); h.Add(Epidemics); h.Add(Raids); h.Add(Walls); h.Add(Tributes); h.Add(Founded); h.Add(Abandoned); h.Add(Found); h.Add(Lost); h.Add(Temp);
        }
    }

    /// <summary>
    /// The world's annals (D2-077; 11 §8): each year's people and places, its births and deaths by cause, its famines, epidemics,
    /// raids, walls, tributes, foundings and abandonments, techniques found and lost, and the planet's mean temperature - kept
    /// at the year's end from the world's own counters. Historians (the Chronicle) read eras from them after the fact; nothing in
    /// the simulation reads them.
    /// </summary>
    public sealed class Annals
    {
        readonly List<AnnalYear> _years = new List<AnnalYear>();
        // the counters at the last year's end (each year is their difference)
        long _births, _deaths, _hunger, _sick, _wounds, _disaster, _famines, _epidemics, _raids, _walls, _tributes, _founded, _found, _lost;

        public int Count => _years.Count;
        public AnnalYear this[int i] => _years[i];

        /// <summary>The year just ended (at the world's yearly housekeeping).</summary>
        public void Year(World w)
        {
            var m = w.Metrics;
            long now = w.Now;
            long disaster = m.EruptionDeaths + m.QuakeDeaths + m.TsunamiDeaths + m.StormDeaths;
            int abandoned = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                long a = w.Settlements.Get(sid).Abandoned;
                if (a != 0 && a > now - SimTime.Year && a <= now) abandoned++;
            }
            _years.Add(new AnnalYear
            {
                Year = (int)(now / SimTime.Year) - 1,
                People = w.LivingPeople(), Places = w.InhabitedSettlements(),
                Births = (int)(m.Births - _births), Deaths = (int)(m.Deaths - _deaths), Hunger = (int)(m.HungerDeaths - _hunger), Sick = (int)(m.DiseaseDeaths - _sick),
                Wounds = (int)(m.WoundDeaths - _wounds), Disaster = (int)(disaster - _disaster), Famines = (int)(m.Famines - _famines), Epidemics = (int)(m.Epidemics - _epidemics),
                Raids = (int)(m.Raids - _raids), Walls = (int)(m.WallsPlanned - _walls), Tributes = (int)(m.Tributes - _tributes), Founded = (int)(m.Foundings - _founded),
                Abandoned = abandoned, Found = (int)(m.TechniquesFound - _found), Lost = (int)(m.TechniquesLost - _lost), Temp = (float)w.Planet.TGlobal,
            });
            _births = m.Births; _deaths = m.Deaths; _hunger = m.HungerDeaths; _sick = m.DiseaseDeaths; _wounds = m.WoundDeaths; _disaster = disaster;
            _famines = m.Famines; _epidemics = m.Epidemics; _raids = m.Raids; _walls = m.WallsPlanned; _tributes = m.Tributes; _founded = m.Foundings;
            _found = m.TechniquesFound; _lost = m.TechniquesLost;
        }

        /// <summary>Premise (tests): a year as given.</summary>
        public void Push(AnnalYear y) => _years.Add(y);

        public void Write(BinWriter w)
        {
            w.Long(_births); w.Long(_deaths); w.Long(_hunger); w.Long(_sick); w.Long(_wounds); w.Long(_disaster); w.Long(_famines); w.Long(_epidemics);
            w.Long(_raids); w.Long(_walls); w.Long(_tributes); w.Long(_founded); w.Long(_found); w.Long(_lost);
            w.Int(_years.Count);
            for (int i = 0; i < _years.Count; i++) _years[i].Write(w);
        }

        public void Read(BinReader r)
        {
            _births = r.Long(); _deaths = r.Long(); _hunger = r.Long(); _sick = r.Long(); _wounds = r.Long(); _disaster = r.Long(); _famines = r.Long(); _epidemics = r.Long();
            _raids = r.Long(); _walls = r.Long(); _tributes = r.Long(); _founded = r.Long(); _found = r.Long(); _lost = r.Long();
            _years.Clear();
            int n = r.Int();
            for (int i = 0; i < n; i++) _years.Add(AnnalYear.Read(r));
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(_births); h.Add(_deaths); h.Add(_hunger); h.Add(_sick); h.Add(_wounds); h.Add(_disaster); h.Add(_famines); h.Add(_epidemics);
            h.Add(_raids); h.Add(_walls); h.Add(_tributes); h.Add(_founded); h.Add(_found); h.Add(_lost);
            h.Add(_years.Count);
            for (int i = 0; i < _years.Count; i++) _years[i].Hash(ref h);
        }
    }
}
