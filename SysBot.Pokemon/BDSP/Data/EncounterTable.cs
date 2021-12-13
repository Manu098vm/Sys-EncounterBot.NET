using System.Collections.Generic;

namespace SysBot.Pokemon
{

    public class MGameObject
    {
        public int m_FileID { get; set; }
        public int m_PathID { get; set; }
    }

    public class MScript
    {
        public int m_FileID { get; set; }
        public long m_PathID { get; set; }
    }

    public class GroundMon
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class Tairyo
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class Day
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class Night
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class SwayGrass
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class GbaRuby
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class GbaSapp
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class GbaEme
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class GbaFire
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class GbaLeaf
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class WaterMon
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class BoroMon
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class IiMon
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class SugoiMon
    {
        public int maxlv { get; set; }
        public int minlv { get; set; }
        public int monsNo { get; set; }
    }

    public class Table
    {
        public int zoneID { get; set; }
        public int encRate_gr { get; set; }
        public IList<GroundMon> ground_mons { get; set; }
        public IList<Tairyo> tairyo { get; set; }
        public IList<Day> day { get; set; }
        public IList<Night> night { get; set; }
        public IList<SwayGrass> swayGrass { get; set; }
        public IList<int> FormProb { get; set; }
        public IList<int> Nazo { get; set; }
        public IList<int> AnnoonTable { get; set; }
        public IList<GbaRuby> gbaRuby { get; set; }
        public IList<GbaSapp> gbaSapp { get; set; }
        public IList<GbaEme> gbaEme { get; set; }
        public IList<GbaFire> gbaFire { get; set; }
        public IList<GbaLeaf> gbaLeaf { get; set; }
        public int encRate_wat { get; set; }
        public IList<WaterMon> water_mons { get; set; }
        public int encRate_turi_boro { get; set; }
        public IList<BoroMon> boro_mons { get; set; }
        public int encRate_turi_ii { get; set; }
        public IList<IiMon> ii_mons { get; set; }
        public int encRate_sugoi { get; set; }
        public IList<SugoiMon> sugoi_mons { get; set; }
    }

    public class Urayama
    {
        public int monsNo { get; set; }
    }

    public class Mistu
    {
        public int Rate { get; set; }
        public int Normal { get; set; }
        public int Rare { get; set; }
        public int SuperRare { get; set; }
    }

    public class Honeytree
    {
        public int Normal { get; set; }
        public int Rare { get; set; }
    }

    public class Safari
    {
        public int MonsNo { get; set; }
    }

    public class Mvpoke
    {
        public int zoneID { get; set; }
        public int nextCount { get; set; }
        public IList<int> nextZoneID { get; set; }
    }

    public class Legendpoke
    {
        public int monsNo { get; set; }
        public int formNo { get; set; }
        public int isFixedEncSeq { get; set; }
        public string encSeq { get; set; }
        public int isFixedBGM { get; set; }
        public string bgmEvent { get; set; }
        public int isFixedBtlBg { get; set; }
        public int btlBg { get; set; }
        public int isFixedSetupEffect { get; set; }
        public int setupEffect { get; set; }
    }

    public class Zui
    {
        public int zoneID { get; set; }
        public IList<int> form { get; set; }
    }

    public class EncounterTable
    {
        public MGameObject m_GameObject { get; set; }
        public int m_Enabled { get; set; }
        public MScript m_Script { get; set; }
        public string m_Name { get; set; }
        public IList<Table> table { get; set; }
        public IList<Urayama> urayama { get; set; }
        public IList<Mistu> mistu { get; set; }
        public IList<Honeytree> honeytree { get; set; }
        public IList<Safari> safari { get; set; }
        public IList<Mvpoke> mvpoke { get; set; }
        public IList<Legendpoke> legendpoke { get; set; }
        public IList<Zui> zui { get; set; }
    }

}
