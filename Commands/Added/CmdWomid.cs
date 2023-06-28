// Command does absolutely nothing, only here to make ClassiCube client happy.
namespace MCForge
{

    public class CmdWomid : Command
    {
        public override string name { get { return "womid"; } }
        public override string shortcut { get { return "womid"; } }
        public override bool museumUsable { get { return true; } }
        public override string type { get { return ""; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Banned; } }

        public override void Use(Player p, string message)
        {
            Player.SendMessage(p, "");
            p.UsingWom = true;
        }
        public override void Help(Player p)
        {
            Player.SendMessage(p, "/womid - Command does absolutely nothing, only here to make ClassiCube client happy.");
        }
    }
}