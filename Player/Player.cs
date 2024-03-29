/*
    Copyright © 2011-2014 MCForge-Redux
		
    Dual-licensed under the	Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
	
    http://www.opensource.org/licenses/ecl2.php
    http://www.gnu.org/licenses/gpl-3.0.html
	
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
*/
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using System.Threading;
using Newtonsoft.Json.Linq;


namespace MCForge
{
    public partial class Player : IDisposable
    {
        public PlayerIgnores Ignores = new PlayerIgnores();

        public string FormatNick(string name)
        {
            Player target = PlayerInfo.FindExact(name);
            // TODO: select color from database?
            if (target != null) return FormatNick(target);

            return Group.GroupIn(name).Color + Server.ToRawUsername(name);
        }

        /// <summary> Formats a player's name for displaying in chat. </summary>        
        public string FormatNick(Player target)
        {
            if (Ignores.Nicks) return target.color + target.truename;
            return target.color + target.DisplayName;
        }
        private static readonly char[] UnicodeReplacements = " ☺☻♥♦♣♠•◘○\n♂♀♪♫☼►◄↕‼¶§▬↨↑↓→←∟↔▲▼".ToCharArray();

        /// <summary> List of chat keywords, and emotes that they stand for. </summary>
        public Dictionary<string, object> ExtraData = new Dictionary<string, object>();
        public static readonly Dictionary<string, char> EmoteKeywords = new Dictionary<string, char> {
            { "darksmile", '\u0001' },

            { "smile", '\u0002' }, // ☻

            { "heart", '\u0003' }, // ♥
            { "hearts", '\u0003' },

            { "diamond", '\u0004' }, // ♦
            { "diamonds", '\u0004' },
            { "rhombus", '\u0004' },

            { "club", '\u0005' }, // ♣
            { "clubs", '\u0005' },
            { "clover", '\u0005' },
            { "shamrock", '\u0005' },

            { "spade", '\u0006' }, // ♠
            { "spades", '\u0006' },

            { "*", '\u0007' }, // •
            { "bullet", '\u0007' },
            { "dot", '\u0007' },
            { "point", '\u0007' },

            { "hole", '\u0008' }, // ◘

            { "circle", '\u0009' }, // ○
            { "o", '\u0009' },

            { "male", '\u000B' }, // ♂
            { "mars", '\u000B' },

            { "female", '\u000C' }, // ♀
            { "venus", '\u000C' },

            { "8", '\u000D' }, // ♪
            { "note", '\u000D' },
            { "quaver", '\u000D' },

            { "notes", '\u000E' }, // ♫
            { "music", '\u000E' },

            { "sun", '\u000F' }, // ☼
            { "celestia", '\u000F' },

            { ">>", '\u0010' }, // ►
            { "right2", '\u0010' },

            { "<<", '\u0011' }, // ◄
            { "left2", '\u0011' },

            { "updown", '\u0012' }, // ↕
            { "^v", '\u0012' },

            { "!!", '\u0013' }, // ‼

            { "p", '\u0014' }, // ¶
            { "para", '\u0014' },
            { "pilcrow", '\u0014' },
            { "paragraph", '\u0014' },

            { "s", '\u0015' }, // §
            { "sect", '\u0015' },
            { "section", '\u0015' },

            { "-", '\u0016' }, // ▬
            { "_", '\u0016' },
            { "bar", '\u0016' },
            { "half", '\u0016' },

            { "updown2", '\u0017' }, // ↨
            { "^v_", '\u0017' },

            { "^", '\u0018' }, // ↑
            { "up", '\u0018' },

            { "v", '\u0019' }, // ↓
            { "down", '\u0019' },

            { ">", '\u001A' }, // →
            { "->", '\u001A' },
            { "right", '\u001A' },

            { "<", '\u001B' }, // ←
            { "<-", '\u001B' },
            { "left", '\u001B' },

            { "l", '\u001C' }, // ∟
            { "angle", '\u001C' },
            { "corner", '\u001C' },

            { "<>", '\u001D' }, // ↔
            { "<->", '\u001D' },
            { "leftright", '\u001D' },

            { "^^", '\u001E' }, // ▲
            { "up2", '\u001E' },

            { "vv", '\u001F' }, // ▼
            { "down2", '\u001F' },

            { "house", '\u007F' } // ⌂
        };

        public static string ReplaceEmoteKeywords(string message)
        {
            if (message == null)
                throw new ArgumentNullException("message");
            int startIndex = message.IndexOf('(');
            if (startIndex == -1)
            {
                return message; // break out early if there are no opening braces
            }

            StringBuilder output = new StringBuilder(message.Length);
            int lastAppendedIndex = 0;
            while (startIndex != -1)
            {
                int endIndex = message.IndexOf(')', startIndex + 1);
                if (endIndex == -1)
                {
                    break; // abort if there are no more closing braces
                }

                // see if emote was escaped (if odd number of backslashes precede it)
                bool escaped = false;
                for (int i = startIndex - 1; i >= 0 && message[i] == '\\'; i--)
                {
                    escaped = !escaped;
                }
                // extract the keyword
                string keyword = message.Substring(startIndex + 1, endIndex - startIndex - 1);
                char substitute;
                if (EmoteKeywords.TryGetValue(keyword.ToLowerInvariant(), out substitute))
                {
                    if (escaped)
                    {
                        // it was escaped; remove escaping character
                        startIndex++;
                        output.Append(message, lastAppendedIndex, startIndex - lastAppendedIndex - 2);
                        lastAppendedIndex = startIndex - 1;
                    }
                    else
                    {
                        // it was not escaped; insert substitute character
                        output.Append(message, lastAppendedIndex, startIndex - lastAppendedIndex);
                        output.Append(substitute);
                        startIndex = endIndex + 1;
                        lastAppendedIndex = startIndex;
                    }
                }
                else
                {
                    startIndex++; // unrecognized macro, keep going
                }
                startIndex = message.IndexOf('(', startIndex);
            }
            // append the leftovers
            output.Append(message, lastAppendedIndex, message.Length - lastAppendedIndex);
            return output.ToString();
        }


        private static readonly Regex EmoteSymbols = new Regex("[\x00-\x1F\x7F☺☻♥♦♣♠•◘○\n♂♀♪♫☼►◄↕‼¶§▬↨↑↓→←∟↔▲▼⌂]");
        public void ClearChat() { OnChat = null; }
        /// <summary>
        /// List of all server players.
        /// </summary>
        public static List<Player> players = new List<Player>();
        /// <summary>
        /// Key - Name
        /// Value - IP
        /// All players who have left this restart.
        /// </summary>
        public static Dictionary<string, string> left = new Dictionary<string, string>();
        /// <summary>
        /// 
        /// </summary>
        public static List<Player> connections = new List<Player>(Server.players);
        System.Timers.Timer muteTimer = new System.Timers.Timer(1000);
        public static List<string> emoteList = new List<string>();
        public List<string> listignored = new List<string>();
        public List<string> mapgroups = new List<string>();
        public static List<string> globalignores = new List<string>();
        public static int totalMySQLFailed = 0;
        public static int number2 = (byte)(players.Count);
        public static byte number { get { return (byte)players.Count; } }
        static System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
        static MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
        public static string lastMSG = "";
        //For CMDs and plugins with teams in them
        public bool isOnRedTeam = false;
        public bool isOnBlueTeam = false;
        public int health = 100;

        public static bool storeHelp = false;
        public static string storedHelp = "";
        public string truename;
        internal bool dontmindme = false;
        public Socket socket;
        System.Timers.Timer timespent = new System.Timers.Timer(1000);
        System.Timers.Timer loginTimer = new System.Timers.Timer(1000);
        public System.Timers.Timer pingTimer = new System.Timers.Timer(2000);
        System.Timers.Timer extraTimer = new System.Timers.Timer(22000);
        public System.Timers.Timer afkTimer = new System.Timers.Timer(2000);
        public int afkCount = 0;
        public DateTime afkStart;
        public string WoMVersion = "";
        public bool megaBoid = false;
        public bool cmdTimer = false;
        public bool UsingWom = false;


        byte[] buffer = new byte[0];
        byte[] tempbuffer = new byte[0xFF];
        public bool disconnected = false;
        public string time;
        public string name;
        public string DisplayName;
        public string SkinName;
        public bool identified = false;
        public bool UsingID = false;
        public int ID = 0;
        public int warn = 0;
        public byte id;
        public int userID = -1;
        public string ip;
        public string exIP; // external IP
        public string color;
        public Group group;
        public bool hidden = false;
        public bool painting = false;
        public bool muted = false;
        public bool jailed = false;
        public bool agreed = false;
        public bool invincible = false;
        public string prefix = "";
        public string title = "";
        public string titlecolor;
        public int TotalMessagesSent = 0;
        public int passtries = 0;
        public int ponycount = 0;
        public int rdcount = 0;
        public bool hasreadrules = false;
        public bool canusereview = true;
        public int hackWarnings = 0;
        public string model = "humanoid";

        //Gc checks
        public string lastmsg = "";
        public int spamcount = 0, capscount = 0, floodcount = 0, multi = 0;
        public DateTime lastmsgtime = DateTime.MinValue;
        /// <summary>
        /// Console only please
        /// </summary>
        public bool canusegc = true;

        //Pyramid Code

        public int pyramidx1;
        public int pyramidx2;
        public int pyramidy1;
        public int pyramidy2;
        public int pyramidz1;
        public int pyramidz2;
        public string pyramidblock;
        public int pyramidtotal;
        public int pyramidtotal2;
        public bool pyramidsilent = false;

        public bool deleteMode = false;
        public bool ignorePermission = false;
        public bool ignoreGrief = false;
        public bool parseSmiley = true;
        public bool smileySaved = true;
        public bool opchat = false;
        public bool adminchat = false;
        public bool onWhitelist = false;
        public bool whisper = false;
        public string whisperTo = "";
        public bool ignoreglobal = false;

        public string storedMessage = "";

        public bool trainGrab = false;
        public bool onTrain = false;
        public bool allowTnt = true;

        public bool frozen = false;
        public string following = "";
        public string possess = "";

        // Only used for possession.
        //Using for anything else can cause unintended effects!
        public bool canBuild = true;

        public int money = 0;
        public int points = 0;
        public long overallBlocks = 0;

        public int loginBlocks = 0;

        public DateTime timeLogged;
        public DateTime firstLogin;
        public int totalLogins = 0;
        public int totalKicked = 0;
        public int overallDeath = 0;

        public string savedcolor = "";

        public bool staticCommands = false;

        public DateTime ZoneSpam;
        public bool ZoneCheck = false;
        public bool zoneDel = false;

        public Thread commThread;
        public bool commUse = false;

        public bool aiming;
        public bool isFlying = false;

        public bool joker = false;
        public bool adminpen = false;

        public bool voice = false;
        public string voicestring = "";

        public int grieferStoneWarn = 0;

        //CTF
        public int tntSeconds = 0;
        public int lazorSeconds = 0;
        public int lightSeconds = 0;
        public int pteam = 0;
        public bool isHoldingFlag = false;
        public bool killingPeople = false;
        public int amountKilled = 0;
        public int overallKilled = 0;
        public int overallDied = 0;
        public ushort[] minePlacement = { 0, 0, 0 };
        public int minesPlaced = 0;
        public ushort[] trapPlacement = { 0, 0, 0 };
        public int trapsPlaced = 0;
        public ushort[] tntPlacement = { 0, 0, 0 };
        public int tntPlaced = 0;
        //public static System.Timers.Timer thetimer3;
        public System.Timers.Timer deathTimer;
        public static System.Timers.Timer lazerTimer;
        public System.Timers.Timer freezeTimer;
        public ushort[] lazerPos = { 0, 0, 0 };
        public bool shotSecondLazer = false;
        public bool deathTimerOn = false;
        public bool hasBeenTrapped = false;
        public bool autoTNT = true;
        public bool ironman = false;
        public bool teamchat = false;
        public bool PlacedNukeThisRound = false;
        public bool BoughtOneUpThisRound = false;
        public ushort[] tripwire1 = { 0, 0, 0 };
        public ushort[] tripwire2 = { 0, 0, 0 };
        public int tripwiresPlaced = 0;
        public int tags = 0;
        public int games = 0;
        public int losses = 0;
        public int wins = 0;

        public void resetDeathTimer(object sender, ElapsedEventArgs e)
        {
            deathTimerOn = false;
            deathTimer.Dispose();
            deathTimer.Enabled = false;
            deathTimer.Stop();
        }

        //items and upgrades
        public int lazers = 0;
        public int lightnings = 0;
        public int traps = 0;
        public int lines = 0;
        public int rockets = 0;
        public int grapple = 0;
        public int bigtnt = 0;
        public int nuke = 0;
        public int jetpack = 0;
        public int tripwire = 0;
        public int knife = 0;
        public int freeze = 0;

        public int lazerUpgrade = 0;
        public int lightningUpgrade = 0;
        public int trapUpgrade = 0;
        public int rocketUpgrade = 0;
        public int tntUpgrade = 0;
        public int pistolUpgrade = 0;
        public int mineUpgrade = 0;
        public int tripwireUpgrade = 0;
        public int knifeUpgrade = 0;

        //buffs
        public bool untouchable = false;
        public bool iceshield = false;
        public bool invinciblee = false;
        public bool makeaura = false;
        public bool clearview = false;
        public bool oneup = false;

        public static System.Timers.Timer untouchableTimer;
        public static System.Timers.Timer iceshieldTimer;
        public static System.Timers.Timer invisibleTimer;
        public static System.Timers.Timer makeauraTimer;
        public static System.Timers.Timer invincibleTimer;

        public EXPLevel explevel;

        //Countdown
        public bool playerofcountdown = false;
        public bool incountdown = false;
        public ushort countdowntempx;
        public ushort countdowntempz;
        public bool countdownsettemps = false;

        //Zombie
        public string Original = "";
        public bool referee = false;
        public int blockCount = 50;
        public bool voted = false;
        public int blocksStacked = 0;
        public int infectThisRound = 0;
        public int lastYblock = 0;
        public int lastXblock = 0;
        public int lastZblock = 0;
        public bool infected = false;
        public bool aka = false;
        public bool flipHead = true;
        public int playersInfected = 0;
        public int NoClipcount = 0;

        //LAVA extra
        public int spongesLeft = 0;
        public bool ironmanActivated = false;
        public bool ironmanFailed = false;
        public int lives = 3;
        public int countOfTimes = 0;
        public bool gotInvite = false;
        public bool sentInvite = false;
        public string sentInvitePlayer = "";
        public string goToPlayer = "";

        /*
        //SMP Mode
        public bool InSMP = false;

        public uint rock = 0;
        public uint grass = 0;
        public uint dirt = 0;
        public uint stone = 0;
        public uint wood = 0;
        public uint shrub = 0;
        public uint blackrock = 0;// adminium
        public uint water = 0;
        public uint waterstill = 0;
        public uint lava = 0;
        public uint lavastill = 0;
        public uint sand = 0;
        public uint gravel = (uint)0;
        public uint goldrock = (uint)0;
        public uint ironrock = (uint)0;
        public uint coal = (uint)0;
        public uint trunk = (uint)0;
        public uint leaf = (uint)0;
        public uint sponge = (uint)0;
        public uint glass = (uint)0;
        public uint red = (uint)0;
        public uint orange = (uint)0;
        public uint yellow = (uint)0;
        public uint lightgreen = (uint)0;
        public uint green = (uint)0;
        public uint aquagreen = (uint)0;
        public uint cyan = (uint)0;
        public uint lightblue = (uint)0;
        public uint blue = (uint)0;
        public uint purple = (uint)0;
        public uint lightpurple = (uint)0;
        public uint pink = (uint)0;
        public uint darkpink = (uint)0;
        public uint darkgrey = (uint)0;
        public uint lightgrey = (uint)0;
        public uint white = (uint)0;
        public uint yellowflower = (uint)0;
        public uint redflower = (uint)0;
        public uint mushroom = (uint)0;
        public uint redmushroom = (uint)0;
        public uint goldsolid = (uint)0;
        public uint iron = (uint)0;
        public uint staircasefull = (uint)0;
        public uint staircasestep = (uint)0;
        public uint brick = (uint)0;
        public uint tnt = (uint)0;
        public uint bookcase = (uint)0;
        public uint stonevine = (uint)0;
        public uint obsidian = (uint)0;
        public uint cobblestoneslab = (uint)0;
        public uint rope = (uint)0;
        public uint sandstone = (uint)0;
        public uint snowreal = (uint)0;
        public uint firereal = (uint)0;
        public uint lightpinkwool = (uint)0;
        public uint forestgreenwool = (uint)0;
        public uint brownwool = (uint)0;
        public uint deepblue = (uint)0;
        public uint turquoise = (uint)0;
        public uint ice = (uint)0;
        public uint ceramictile = (uint)0;
        public uint magmablock = (uint)0;
        public uint pillar = (uint)0;
        public uint crate = (uint)0;
        public uint stonebrick = (uint)0;
        */
        public bool spawned = false;

        //Tnt Wars
        public bool PlayingTntWars = false;
        public int CurrentAmountOfTnt = 0;
        public int CurrentTntGameNumber; //For keeping track of which game is which
        public int TntWarsHealth = 2;
        public int TntWarsKillStreak = 0;
        public float TntWarsScoreMultiplier = 1f;
        public int TNTWarsLastKillStreakAnnounced = 0;
        public bool inTNTwarsMap = false;
        public Player HarmedBy = null; //For Assists

        //Copy
        public List<CopyPos> CopyBuffer = new List<CopyPos>();
        public struct CopyPos { public ushort x, y, z; public ushort type; }
        public bool copyAir = false;
        public int[] copyoffset = new int[3] { 0, 0, 0 };
        public ushort[] copystart = new ushort[3] { 0, 0, 0 };

        public bool sentCustomBlockSupport = false;

        public bool Mojangaccount
        {
            get
            {
                return truename.Contains('@');
            }
        }

        //Undo
        public struct UndoPos { public ushort x, y, z; public ushort type, newtype; public string mapName; public DateTime timePlaced; }
        public List<UndoPos> UndoBuffer = new List<UndoPos>();
        public List<UndoPos> RedoBuffer = new List<UndoPos>();


        public bool showPortals = false;
        public bool showMBs = false;

        public string prevMsg = "";

        //Block Change variable holding
        public int[] BcVar;


        //Movement
        public ushort oldBlock = 0;
        public ushort deathCount = 0;
        public ushort deathblock;

        //Games
        public DateTime lastDeath = DateTime.Now;

        public byte blockAction; //0-Nothing 1-solid 2-lava 3-water 4-active_lava 5 Active_water 6 OpGlass 7 BluePort 8 OrangePort
        public ushort modeType;
        public ushort[] bindings = new ushort[(ushort)128];
        public string[] cmdBind = new string[10];
        public string[] messageBind = new string[10];
        public string lastCMD = "";
        public sbyte c4circuitNumber = -1;

        public Level level = Server.mainLevel;
        public bool Loading = true; //True if player is loading a map.
        public ushort[] lastClick = new ushort[] { 0, 0, 0 };

        public ushort[] pos = new ushort[] { 0, 0, 0 };
        ushort[] oldpos = new ushort[] { 0, 0, 0 };
        // ushort[] basepos = new ushort[] { 0, 0, 0 };
        public byte[] rot = new byte[] { 0, 0 };
        byte[] oldrot = new byte[] { 0, 0 };

        //ushort[] clippos = new ushort[3] { 0, 0, 0 };
        //byte[] cliprot = new byte[2] { 0, 0 };

        // grief/spam detection
        public static int spamBlockCount = 200;
        public bool isUsingOpenClassic = false;
        public static int spamBlockTimer = 5;
        Queue<DateTime> spamBlockLog = new Queue<DateTime>(spamBlockCount);

        public int consecutivemessages;
        private System.Timers.Timer resetSpamCount = new System.Timers.Timer(Server.spamcountreset * 1000);
        //public static int spamChatCount = 3;
        //public static int spamChatTimer = 4;
        //Queue<DateTime> spamChatLog = new Queue<DateTime>(spamChatCount);

        // CmdVoteKick
        public VoteKickChoice voteKickChoice = VoteKickChoice.HasntVoted;

        // Extra storage for custom commands
        public ExtrasCollection Extras = new ExtrasCollection();

        //Chatrooms
        public string Chatroom;
        public List<string> spyChatRooms = new List<string>();
        public DateTime lastchatroomglobal;

        //Waypoints
        public List<Waypoint.WP> Waypoints = new List<Waypoint.WP>();

        //Random...
        public Random random = new Random();

        //Global Chat
        public bool muteGlobal;

        public bool loggedIn;
        public bool InGlobalChat { get; set; }
        public Dictionary<string, string> sounds = new Dictionary<string, string>();

        public bool isOldDev, isMod, isGCMod, isDev; //is this player an original dev/new dev/mod/gcmod?
        public bool isStaff;
        public bool isProtected;
        public bool verifiedName;

        public string appName;
        public int extensionCount;
        public List<string> extensions = new List<string>();
        public int customBlockSupportLevel;
        public bool extension;

        public struct OfflinePlayer
        {
            public string name, color, title, titleColor;
            public int money;
            //need moar? add moar! just make sure you adjust Player.FindOffline() method
            /// <summary>
            /// Creates a new OfflinePlayer object.
            /// </summary>
            /// <param name="nm">Name of the player.</param>
            /// <param name="clr">Color of player name.</param>
            /// <param name="tl">Title of player.</param>
            /// <param name="tlclr">Title color of player</param>
            /// <param name="mon">Player's money.</param>
            public OfflinePlayer(string nm, string clr, string tl, string tlclr, int mon) { name = nm; color = clr; title = tl; titleColor = tlclr; money = mon; }
        }

        public static string CheckPlayerStatus(Player p)
        {
            if (p.hidden)
                return "hidden";
            if (Server.afkset.Contains(p.name))
                return "afk";
            return "active";
        }
        public bool Readgcrules = false;
        public DateTime Timereadgcrules = DateTime.MinValue;
        public bool CheckIfInsideBlock()
        {
            return CheckIfInsideBlock(this);
        }

        public static bool CheckIfInsideBlock(Player p)
        {
            ushort x, y, z;
            x = (ushort)(p.pos[0] / 32);
            y = (ushort)(p.pos[1] / 32);
            y = (ushort)Math.Round((decimal)(((y * 32) + 4) / 32));
            z = (ushort)(p.pos[2] / 32);

            ushort b = p.level.GetTile(x, y, z);
            ushort b1 = p.level.GetTile(x, (ushort)(y - 1), z);

            if (Block.Walkthrough(Block.Convert(b)) && Block.Walkthrough(Block.Convert(b1)))
            {
                return false;
            }
            return Block.Convert(b) != Block.Zero && Block.Convert(b) != Block.op_air;
        }

        //This is so that plugin devs can declare a player without needing a socket..
        //They would still have to do p.Dispose()..
        public Player(string playername) { name = playername; }

        public NetworkStream Stream;
        public BinaryReader Reader;

        public Player(Socket s)
        {
            try
            {
                socket = s;
                ip = socket.RemoteEndPoint.ToString().Split(':')[0];

                if (IPInPrivateRange(ip))
                    exIP = ResolveExternalIP(ip);
                else
                    exIP = ip;

                Server.s.Log(name + "(" + ip + ")" + " connected to the server.");

                for (byte i = 0; i < 128; ++i) bindings[i] = i;

                socket.BeginReceive(tempbuffer, 0, tempbuffer.Length, SocketFlags.None, new AsyncCallback(Receive), this);
                timespent.Elapsed += delegate
                {
                    if (!Loading)
                    {
                        try
                        {
                            int Days = Convert.ToInt32(time.Split(' ')[0]);
                            int Hours = Convert.ToInt32(time.Split(' ')[1]);
                            int Minutes = Convert.ToInt32(time.Split(' ')[2]);
                            int Seconds = Convert.ToInt32(time.Split(' ')[3]);
                            Seconds++;
                            if (Seconds >= 60)
                            {
                                Minutes++;
                                Seconds = 0;
                            }
                            if (Minutes >= 60)
                            {
                                Hours++;
                                Minutes = 0;
                            }
                            if (Hours >= 24)
                            {
                                Days++;
                                Hours = 0;
                            }
                            time = "" + Days + " " + Hours + " " + Minutes + " " + Seconds;
                        }
                        catch { time = "0 0 0 1"; }
                    }
                };
                timespent.Start();
                loginTimer.Elapsed += delegate
                {
                    if (!Loading)
                    {
                        loginTimer.Stop();
                        if (File.Exists("text/welcome.txt"))
                        {
                            try
                            {
                                using (StreamReader wm = File.OpenText("text/welcome.txt"))
                                {
                                    List<string> welcome = new List<string>();
                                    while (!wm.EndOfStream)
                                        welcome.Add(wm.ReadLine());
                                    foreach (string w in welcome)
                                        SendMessage(w);
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            Server.s.Log("Could not find welcome.txt. Using default.");
                            File.WriteAllText("text/welcome.txt", "Welcome to my server!");
                            SendMessage("Welcome to my server!");
                        }
                        extraTimer.Start();
                        loginTimer.Dispose();
                    }
                }; loginTimer.Start();

                pingTimer.Elapsed += delegate { SendPing(); };
                pingTimer.Start();

                extraTimer.Elapsed += delegate
                {
                    extraTimer.Stop();

                    if (Server.updateTimer.Interval > 1000) SendMessage("Lowlag mode is currently &aON.");
                    try
                    {
                        if (!Group.Find("Nobody").commands.Contains("pay") && !Group.Find("Nobody").commands.Contains("give") && !Group.Find("Nobody").commands.Contains("take")) SendMessage("You currently have &a" + money + Server.DefaultColor + " " + Server.moneys);
                    }
                    catch { }
                    SendMessage("You have modified &a" + overallBlocks + Server.DefaultColor + " blocks!");
                    if (players.Count == 1)
                        SendMessage("There is currently &a" + players.Count + " player online.");
                    else
                        SendMessage("There are currently &a" + players.Count + " players online.");
                    try
                    {
                        if (!Group.Find("Nobody").commands.Contains("award") && !Group.Find("Nobody").commands.Contains("awards") && !Group.Find("Nobody").commands.Contains("awardmod")) SendMessage("You have " + Awards.awardAmount(name) + " awards.");
                    }
                    catch { }
                    try
                    {
                        ZombieGame.alive.Remove(this);
                        ZombieGame.infectd.Remove(this);
                    }
                    catch { }
                    if (Server.lava.active) SendMessage("There is a &aLava Survival " + Server.DefaultColor + "game active! Join it by typing /ls go");
                    extraTimer.Dispose();
                };

                afkTimer.Elapsed += delegate
                {
                    if (name == "") return;

                    if (Server.afkset.Contains(name))
                    {
                        afkCount = 0;
                        if (Server.afkkick > 0 && group.Permission < Server.afkkickperm)
                            if (afkStart.AddMinutes(Server.afkkick) < DateTime.Now)
                                Kick("Auto-kick, AFK for " + Server.afkkick + " minutes");
                        if ((oldpos[0] != pos[0] || oldpos[1] != pos[1] || oldpos[2] != pos[2]) && (oldrot[0] != rot[0] || oldrot[1] != rot[1]))
                            Command.all.Find("afk").Use(this, "");
                    }
                    else
                    {
                        if (oldpos[0] == pos[0] && oldpos[1] == pos[1] && oldpos[2] == pos[2] && oldrot[0] == rot[0] && oldrot[1] == rot[1])
                            afkCount++;
                        else
                            afkCount = 0;

                        if (afkCount > Server.afkminutes * 30)
                        {
                            if (name != null && !String.IsNullOrEmpty(name.Trim()))
                            {
                                Command.all.Find("afk").Use(this, "auto: Not moved for " + Server.afkminutes + " m");
                                if (AFK != null)
                                    AFK(this);
                                if (ONAFK != null)
                                    ONAFK(this);
                                OnPlayerAFKEvent.Call(this);
                                afkCount = 0;
                            }
                        }
                    }
                };
                resetSpamCount.Elapsed += delegate
                {
                    if (consecutivemessages > 0)
                        consecutivemessages = 0;
                };
                resetSpamCount.Start();

                if (Server.afkminutes > 0) afkTimer.Start();

                connections.Add(this);
            }
            catch (Exception e) { Kick("Login failed!"); Server.ErrorLog(e); }
        }

        public DateTime lastlogin;
        public void save()
        {
            PlayerDB.Save(this);
            EXPDB.Save(this);

            try
            {
                if (!smileySaved)
                {
                    if (parseSmiley)
                        emoteList.RemoveAll(s => s == name);
                    else
                        emoteList.Add(name);

                    File.WriteAllLines("text/emotelist.txt", emoteList.ToArray());
                    smileySaved = true;
                }
            }
            catch (Exception e)
            {
                Server.ErrorLog(e);
            }
            try
            {
                SaveUndo();
            }
            catch (Exception e)
            {
                Server.s.Log("Error saving undo data.");
                Server.ErrorLog(e);
            }
        }

        #region == INCOMING ==
        static void Receive(IAsyncResult result)
        {
            //Server.s.Log(result.AsyncState.ToString());
            Player p = (Player)result.AsyncState;
            if (p.disconnected || p.socket == null)
                return;
            try
            {
                int length = p.socket.EndReceive(result);
                if (length == 0) { p.Disconnect(); return; }

                byte[] b = new byte[p.buffer.Length + length];
                Buffer.BlockCopy(p.buffer, 0, b, 0, p.buffer.Length);
                Buffer.BlockCopy(p.tempbuffer, 0, b, p.buffer.Length, length);

                p.buffer = p.HandleMessage(b);
                if (p.dontmindme && p.buffer.Length == 0)
                {
                    Server.s.Log("Disconnected");
                    p.socket.Close();
                    p.disconnected = true;
                    return;
                }
                if (!p.disconnected)
                    p.socket.BeginReceive(p.tempbuffer, 0, p.tempbuffer.Length, SocketFlags.None,
                                          new AsyncCallback(Receive), p);
            }
            catch (SocketException)
            {
                p.Disconnect();
            }
            catch (ObjectDisposedException)
            {
                // Player is no longer connected, socket was closed
                // Mark this as disconnected and remove them from active connection list
                Player.SaveUndo(p);
                if (connections.Contains(p))
                    connections.Remove(p);
                p.disconnected = true;
            }
            catch (Exception e)
            {
                Server.ErrorLog(e);
                p.Kick("Error!");
            }
        }
        byte[] HandleMessage(byte[] buffer)
        {
            try
            {
                int length = 0; byte msg = buffer[0];
                // Get the length of the message by checking the first byte
                switch (msg)
                {
                    //For wom
                    case (byte)'G':
                        level.textures.ServeCfg(this, buffer);
                        return new byte[1];
                    case 0:
                        length = 130;
                        break; // login
                    case 5:
                        if (!loggedIn)
                            goto default;
                        length = 8;
                        break; // blockchange
                    case 8:
                        if (!loggedIn)
                            goto default;
                        length = 9;
                        break; // input
                    case 13:
                        if (!loggedIn)
                            goto default;
                        length = 65;
                        break; // chat
                    case 16:
                        length = 66;
                        break;
                    case 17:
                        length = 68;
                        break;
                    case 19:
                        length = 1;
                        break;
                    default:
                        if (!dontmindme)
                            Kick("Unhandled message id \"" + msg + "\"!");
                        else
                            Server.s.Log(Encoding.UTF8.GetString(buffer, 0, buffer.Length));
                        return new byte[0];
                }
                if (buffer.Length > length)
                {
                    byte[] message = new byte[length];
                    Buffer.BlockCopy(buffer, 1, message, 0, length);

                    byte[] tempbuffer = new byte[buffer.Length - length - 1];
                    Buffer.BlockCopy(buffer, length + 1, tempbuffer, 0, buffer.Length - length - 1);

                    buffer = tempbuffer;

                    // Thread thread = null;
                    switch (msg)
                    {
                        case 0:
                            HandleLogin(message);
                            break;
                        case 5:
                            if (!loggedIn)
                                break;
                            HandleBlockchange(message);
                            break;
                        case 8:
                            if (!loggedIn)
                                break;
                            HandleInput(message);
                            break;
                        case 13:
                            if (!loggedIn)
                                break;
                            HandleChat(message);
                            break;
                        case 16:
                            HandleExtInfo(message);
                            break;
                        case 17:
                            HandleExtEntry(message);
                            break;
                        case 19:
                            HandleCustomBlockSupportLevel(message);
                            break;
                    }
                    //thread.Start((object)message);
                    if (buffer.Length > 0)
                        buffer = HandleMessage(buffer);
                    else
                        return new byte[0];
                }
            }
            catch (Exception e)
            {
                Server.ErrorLog(e);
            }
            return buffer;
        }

        public void HandleExtInfo(byte[] message)
        {
            appName = enc.GetString(message, 0, 64).Trim();
            extensionCount = message[65];
        }
        public struct CPE { public string name; public int version; }
        public List<CPE> ExtEntry = new List<CPE>();
        void HandleExtEntry(byte[] msg)
        {
            AddExtension(enc.GetString(msg, 0, 64).Trim(), NTHO_Int(msg, 64));
            extensionCount--;
        }

        public static int NTHO_Int(byte[] x, int offset)
        {
            byte[] y = new byte[4];
            Buffer.BlockCopy(x, offset, y, 0, 4); Array.Reverse(y);
            return BitConverter.ToInt32(y, 0);
        }

        public void HandleCustomBlockSupportLevel(byte[] message)
        {
            customBlockSupportLevel = message[0];
        }

        void HandleLogin(byte[] message)
        {
            try
            {
                //byte[] message = (byte[])m;
                if (loggedIn)
                    return;
                byte version = message[0];
                name = enc.GetString(message, 1, 64).Trim();
                DisplayName = name;
                SkinName = name;
                truename = name;
                if (Server.omniban.CheckPlayer(this)) { Kick(Server.omniban.kickMsg); return; } //deprecated
                if (name.Split('@').Length > 1)
                {
                    name = name.Split('@')[0];
                    if (!MojangAccount.HasID(truename))
                        MojangAccount.AddUser(truename);
                    name += "_" + MojangAccount.GetID(truename);
                }
                string verify = enc.GetString(message, 65, 32).Trim();
                ushort type = message[129];

                //Forge Protection Check
                verifiedName = Server.verify ? true : false;
                if (Server.verify)
                {
                    if (verify == BitConverter.ToString(md5.ComputeHash(enc.GetBytes(Server.salt + truename))).Replace("-", "").ToLower().TrimStart('0'))
                    {
                        identified = true;
                    }
                    if (verify == BitConverter.ToString(md5.ComputeHash(enc.GetBytes(Server.salt2 + truename))).Replace("-", "").ToLower())
                    {
                        JObject json;
                        try
                        {
                            using (var client = new WebClient())
                            {

                                json = JObject.Parse(client.DownloadString("http://www.classicube.net/api/player/" + name.ToLower()));
                            }
                            ID = (int)json.SelectToken("id");
                            UsingID = true;
                        }
                        catch (Exception e)
                        {
                            Server.ErrorLog(e);
                            Server.s.Log("Could not get Player's ID, going with name bans!");
                            UsingID = false;
                        }
                        identified = true;
                        name += "+";
                    }
                    if (IPInPrivateRange(ip))
                    {
                        identified = true;
                    }
                    if (identified == false)
                    {
                        Kick("Login failed! Try again."); return;
                    }
                    isDev = Server.Devs.Contains(name.ToLower());
                    isOldDev = Server.oldDevs.Contains(name.ToLower());
                    isMod = Server.Mods.Contains(name.ToLower());
                    isGCMod = Server.GCmods.Contains(name.ToLower());
                    isStaff = isOldDev || isMod || isGCMod || isDev;
                    isProtected = Server.forgeProtection == ForgeProtection.Mod && (isMod || isOldDev) ? true : Server.forgeProtection == ForgeProtection.Dev && isOldDev ? true : false;
                }
                try
                {
                    Server.TempBan tBan = Server.tempBans.Find(tB => tB.name.ToLower() == name.ToLower());

                    if (tBan.allowedJoin < DateTime.Now)
                    {
                        Server.tempBans.Remove(tBan);
                    }
                    else if (!isProtected)
                    {
                        Kick("You're still banned (temporary ban)!");
                    }
                }
                catch { }

                // Whitelist check.
                if (Server.useWhitelist)
                {
                    if (Server.verify)
                    {
                        if (Server.whiteList.Contains(name))
                        {
                            onWhitelist = true;
                        }
                    }
                    else
                    {
                        if (Server.whiteList.Contains(name))
                        {
                            onWhitelist = true;
                        }
                    }
                    onWhitelist = isOldDev || isMod || isDev;
                    if (!onWhitelist) { Kick("This is a private server!"); return; } //i think someone forgot this?
                }


                if (File.Exists("ranks/ignore/" + this.name + ".txt"))
                {
                    try
                    {
                        string[] checklines = File.ReadAllLines("ranks/ignore/" + this.name + ".txt");
                        foreach (string checkline in checklines)
                        {
                            this.listignored.Add(checkline);
                        }
                        File.Delete("ranks/ignore/" + this.name + ".txt");
                    }
                    catch
                    {
                        Server.s.Log("Failed to load ignore list for: " + this.name);
                    }
                }

                if (File.Exists("ranks/ignore/GlobalIgnore.xml"))
                {
                    try
                    {
                        string[] searchxmls = File.ReadAllLines("ranks/ignore/GlobalIgnore.xml");
                        foreach (string searchxml in searchxmls)
                        {
                            globalignores.Add(searchxml);
                        }
                        foreach (string ignorer in globalignores)
                        {
                            Player foundignore = Player.Find(ignorer);
                            foundignore.ignoreglobal = true;
                        }
                        File.Delete("ranks/ignore/GlobalIgnore.xml");
                    }
                    catch
                    {
                        Server.s.Log("Failed to load global ignore list!");
                    }
                }
                // ban check
                if (!isProtected)
                {
                    if (Server.bannedIP.Contains(ip))
                    {
                        if (Server.useWhitelist)
                        {
                            if (!onWhitelist)
                            {
                                Kick(Server.customBanMessage);
                                return;
                            }
                        }
                        else
                        {
                            Kick(Server.customBanMessage);
                            return;
                        }
                    }
                    if (Group.findPlayerGroup(name) == Group.findPerm(LevelPermission.Banned))
                    {
                        if (Server.useWhitelist)
                        {
                            if (!onWhitelist)
                            {
                                Kick(Server.customBanMessage);
                                return;
                            }
                        }
                        else
                        {
                            if (UsingID && Ban.IsbannedID(ID.ToString()) || !UsingID && Ban.Isbanned(name))
                            {
                                string[] data = Ban.Getbandata(name);
                                Kick("You were banned for \"" + data[1] + "\" by " + data[0]);
                            }
                            else
                                Kick(Server.customBanMessage);
                            return;
                        }
                    }
                }

                //server maxplayer check
                if (!isOldDev && !isMod && !VIP.Find(this))
                {
                    // Check to see how many guests we have
                    if (Player.players.Count >= Server.players && !IPInPrivateRange(ip)) { Kick("Server full!"); return; }
                    // Code for limiting no. of guests
                    if (Group.findPlayerGroup(name) == Group.findPerm(LevelPermission.Guest))
                    {
                        // Check to see how many guests we have
                        int currentNumOfGuests = Player.players.Count(pl => pl.group.Permission <= LevelPermission.Guest);
                        if (currentNumOfGuests >= Server.maxGuests)
                        {
                            if (Server.guestLimitNotify) GlobalMessageOps("Guest " + this.name + " couldn't log in - too many guests.");
                            Server.s.Log("Guest " + this.name + " couldn't log in - too many guests.");
                            Kick("Server has reached max number of guests");
                            return;
                        }
                    }
                }

                foreach (Player p in players)
                {
                    if (p.name == name)
                    {
                        if (Server.verify)
                        {
                            p.Kick("Someone logged in as you!"); break;
                        }
                        else { Kick("Already logged in!"); return; }
                    }
                }
                if (type == 0x42)
                {
                    extension = true;
                    SendExtInfo(14);
                    SendExtEntry("ClickDistance", 1);
                    SendExtEntry("CustomBlocks", 1);
                    SendExtEntry("HeldBlock", 1);
                    SendExtEntry("TextHotKey", 1);
                    SendExtEntry("ExtPlayerList", 2);
                    SendExtEntry("EnvColors", 1);
                    SendExtEntry("SelectionCuboid", 1);
                    SendExtEntry("BlockPermissions", 1);
                    SendExtEntry("ChangeModel", 1);
                    SendExtEntry("EnvMapAppearance", 1);
                    SendExtEntry("EnvWeatherType", 1);
                    SendExtEntry("HackControl", 1);
                    SendExtEntry("EmoteFix", 1);
                    SendExtEntry("FullCP437", 1);
                    SendExtEntry("MessageTypes", 1);
                    SendExtEntry("LongerMessages", 1);
                    SendExtEntry("TextHotkey", 1);
                    // All CPE features supported in ClasssiCube:
                    //"ClickDistance", "CustomBlocks", "HeldBlock", "EmoteFix", "TextHotKey", "ExtPlayerList",
                    //"EnvColors", "SelectionCuboid", "BlockPermissions", "ChangeModel", "EnvMapAppearance",
                    //"EnvWeatherType", "MessageTypes", "HackControl", "PlayerClick", "FullCP437", "LongerMessages",
                    //"BlockDefinitions", "BlockDefinitionsExt", "BulkBlockUpdate", "TextColors", "EnvMapAspect",
                    //"EntityProperty", "ExtEntityPositions",  "TwoWayPing", "InventoryOrder", "InstantMOTD", "FastMap", "SetHotbar",
                    //"SetSpawnpoint","VelocityControl", "CustomParticles",  "CustomModels", "PluginMessages", "ExtEntityTeleport",
                    /* NOTE: These must be placed last for when EXTENDED_TEXTURES or EXTENDED_BLOCKS are not defined */
                    //"ExtendedTextures", "ExtendedBlocks"
                    SendCustomBlockSupportLevel(1);
                }


                try { left.Remove(name.ToLower()); }
                catch { }

                group = Group.findPlayerGroup(name);

                SendMotd();
                SendMap();
                Loading = true;

                if (disconnected) return;

                this.id = FreeId();

                lock (players)
                    players.Add(this);

                connections.Remove(this);

                Server.s.PlayerListUpdate();
                //Test code to show when people come back with different accounts on the same IP
                string temp = name + " is lately known as:";
                bool found = false;
                if (!ip.StartsWith("127.0.0."))
                {
                    foreach (KeyValuePair<string, string> prev in left)
                    {
                        if (prev.Value == ip)
                        {
                            found = true;
                            temp += " " + prev.Key;
                        }
                    }
                    if (found)
                    {
                        if (this.group.Permission < Server.adminchatperm || Server.adminsjoinsilent == false)
                        {
                            GlobalMessageOps(temp);
                            //IRCBot.Say(temp, true); //Tells people in op channel on IRC
                        }

                        Server.s.Log(temp);
                    }
                }
            }
            catch (Exception e)
            {
                Server.ErrorLog(e);
                Player.GlobalMessage("An error occurred: " + e.Message);
            }
            //OpenClassic Client Check
            SendBlockchange(0, 0, 0, 0);
            /*Database.AddParams("@Name", name);
            DataTable playerDb = Database.fillData("SELECT * FROM Players WHERE Name=@Name");


            if (playerDb.Rows.Count == 0)
            {
                this.prefix = "";
                this.time = "0 0 0 1";
                this.title = "";
                this.titlecolor = "";
                this.color = group.color;
                this.money = 0;
                this.firstLogin = DateTime.Now;
                this.totalLogins = 1;
                this.totalKicked = 0;
                this.overallDeath = 0;
                this.overallBlocks = 0;

                this.timeLogged = DateTime.Now;
                SendMessage("Welcome " + name + "! This is your first visit.");
                string query = "INSERT INTO Economy (player, money, total, purchase, payment, salary, fine) VALUES ('" + name + "', " + money + ", 0, '%cNone', '%cNone', '%cNone', '%cNone')";
                {
                    SQLite.executeQuery(String.Format("INSERT INTO Players (Name, IP, FirstLogin, LastLogin, totalLogin, Title, totalDeaths, Money, totalBlocks, totalKicked, TimeSpent) VALUES ('{0}', '{1}', '{2:yyyy-MM-dd HH:mm:ss}', '{3:yyyy-MM-dd HH:mm:ss}', {4}, '{5}', {6}, {7}, {8}, {9}, '{10}')", name, ip, firstLogin, DateTime.Now, totalLogins, prefix, overallDeath, money, loginBlocks, totalKicked, time));
                    SQLite.executeQuery(query);
                }
            }
            else
            {
                totalLogins = int.Parse(playerDb.Rows[0]["totalLogin"].ToString()) + 1;
                time = playerDb.Rows[0]["TimeSpent"].ToString();
                userID = int.Parse(playerDb.Rows[0]["ID"].ToString());
                firstLogin = DateTime.Parse(playerDb.Rows[0]["firstLogin"].ToString());
                timeLogged = DateTime.Now;
                if (playerDb.Rows[0]["Title"].ToString().Trim() != "")
                {
                    string parse = playerDb.Rows[0]["Title"].ToString().Trim().Replace("[", "");
                    title = parse.Replace("]", "");
                }
                if (playerDb.Rows[0]["title_color"].ToString().Trim() != "")
                {
                    titlecolor = c.Parse(playerDb.Rows[0]["title_color"].ToString().Trim());
                }
                else
                {
                    titlecolor = "";
                }
                if (playerDb.Rows[0]["color"].ToString().Trim() != "")
                {
                    color = c.Parse(playerDb.Rows[0]["color"].ToString().Trim());
                }
                else
                {
                    color = group.color;
                }
                overallDeath = int.Parse(playerDb.Rows[0]["TotalDeaths"].ToString());
                overallBlocks = long.Parse(playerDb.Rows[0]["totalBlocks"].ToString().Trim());
                money = int.Parse(playerDb.Rows[0]["Money"].ToString());
                //money = Economy.RetrieveEcoStats(this.name).money;
                totalKicked = int.Parse(playerDb.Rows[0]["totalKicked"].ToString());*/
            if (!Directory.Exists("players"))
            {
                Directory.CreateDirectory("players");
            }
            PlayerDB.Load(this);
            SendMessage("Welcome back " + color + prefix + name + Server.DefaultColor + "! You've been here " + totalLogins + " times!");
            // GlobalMessage(name + "connected");
            {
                if (Server.muted.Contains(name))
                {
                    muted = true;
                    GlobalMessage(name + " is still muted from the last time they went offline.");

                }
            }
            if (!Directory.Exists("exp"))
            {
                Directory.CreateDirectory("exp");
            }
            EXPDB.Load(this);
            if (!Directory.Exists("players/economy"))
            {
                Directory.CreateDirectory("players/economy");
            }
            Economy.EcoStats es = new Economy.EcoStats();
            es.playerName = this.name;
            EconomyDB.Load(es);
            SetPrefix();

            if (PlayerConnect != null)
                PlayerConnect(this);
            OnPlayerConnectEvent.Call(this);

            if (Server.Owner != "" && Server.Owner.ToLower().Equals(this.name.ToLower()))
            {
                if (color == Group.standard.color)
                {
                    color = "&c";
                }
                if (title == "")
                {
                    title = "Owner";
                }
                SetPrefix();
            }
            //    playerDb.Dispose();
            //Re-implenting MCLawl-Era Dev recognition. Is harmless and does little, but is still nice. 
            if (isOldDev)
            {
                if (color == Group.standard.color)
                {
                    color = "&9";
                }
                if (prefix == "")
                {
                    title = "Dev";
                }
                SetPrefix();
                Readgcrules = true; //Devs should know the rules. 
            }

            if (!spawned)
            {
                try
                {
                    ushort x = (ushort)((0.5 + level.spawnx) * 32);
                    ushort y = (ushort)((1 + level.spawny) * 32);
                    ushort z = (ushort)((0.5 + level.spawnz) * 32);
                    pos = new ushort[3] { x, y, z }; rot = new byte[2] { level.rotx, level.roty };

                    GlobalSpawn(this, x, y, z, rot[0], rot[1], true);
                    foreach (Player p in players)
                    {
                        if (p.level == level && p != this && !p.hidden)
                            SendSpawn(p.id, p.color + p.name, p.pos[0], p.pos[1], p.pos[2], p.rot[0], p.rot[1], p.DisplayName, p.SkinName);
                        if (HasExtension("ChangeModel"))
                        {
                            if (p == this)
                                unchecked { SendChangeModel((byte)-1, model); }
                            else SendChangeModel(p.id, p.model);
                        }
                    }
                    foreach (PlayerBot pB in PlayerBot.playerbots)
                    {
                        if (pB.level == level)
                            SendSpawn(pB.id, pB.color + pB.name, pB.pos[0], pB.pos[1], pB.pos[2], pB.rot[0], pB.rot[1], pB.name, pB.name);
                    }

                }
                catch (Exception e)
                {
                    Server.ErrorLog(e);
                    Server.s.Log("Error spawning player \"" + name + "\"");
                }
                spawned = true;
            }

            Loading = false;

            if (Server.verifyadmins == true)
            {
                if (this.group.Permission >= Server.verifyadminsrank)
                {
                    adminpen = true;
                }
            }
            if (emoteList.Contains(name)) parseSmiley = false;
            if (!Directory.Exists("text/login"))
            {
                Directory.CreateDirectory("text/login");
            }
            if (!File.Exists("text/login/" + this.name + ".txt"))
            {
                File.WriteAllText("text/login/" + this.name + ".txt", "connected.");
            }
            loggedIn = true;
            lastlogin = DateTime.Now;
            //very very sloppy, yes I know.. but works for the time
            //^Perhaps we should update this? -EricKilla
            //which bit is this referring to? - HeroCane

            bool gotoJail = false;
            string gotoJailMap = "";
            string gotoJailName = "";
            try
            {
                if (File.Exists("ranks/jailed.txt"))
                {
                    using (StreamReader read = new StreamReader("ranks/jailed.txt"))
                    {
                        string line;
                        while ((line = read.ReadLine()) != null)
                        {
                            if (line.Split()[0].ToLower() == this.name.ToLower())
                            {
                                gotoJail = true;
                                gotoJailMap = line.Split()[1];
                                gotoJailName = line.Split()[0];
                                break;
                            }
                        }
                    }
                }
                else { File.Create("ranks/jailed.txt").Close(); }
            }
            catch
            {
                gotoJail = false;
            }
            if (gotoJail)
            {
                try
                {
                    Command.all.Find("goto").Use(this, gotoJailMap);
                    Command.all.Find("jail").Use(null, gotoJailName);
                }
                catch (Exception e)
                {
                    Kick(e.ToString());
                }
            }

            if (Server.agreetorulesonentry)
            {
                if (!File.Exists("ranks/agreed.txt"))
                    File.WriteAllText("ranks/agreed.txt", "");
                var agreedFile = File.ReadAllText("ranks/agreed.txt");
                if (this.group.Permission == LevelPermission.Guest)
                {
                    if (!agreedFile.Contains(this.name.ToLower()))
                        SendMessage("&9You must read the &4/rules&9 and &4/agree&9 to them before you can build and use commands!");
                    else agreed = true;
                }
                else { agreed = true; }
            }
            else { agreed = true; }

            string joinm = "&a+ " + this.color + this.prefix + this.name + Server.DefaultColor + " " + File.ReadAllText("text/login/" + this.name + ".txt");
            if (this.group.Permission < Server.adminchatperm || Server.adminsjoinsilent == false)
            {
                if ((Server.guestJoinNotify == true && this.group.Permission <= LevelPermission.Guest) || this.group.Permission > LevelPermission.Guest)
                {
                    Player.players.ForEach(p1 =>
                    {
                        if (p1.UsingWom)
                        {
                            byte[] buffer = new byte[65];
                            Player.StringFormat("^detail.user.join=" + color + name + c.white, 64).CopyTo(buffer, 1);
                            p1.SendRaw(OpCode.Message, buffer);
                            buffer = null;
                            Player.SendMessage(p1, joinm);
                        }
                        else
                            Player.SendMessage(p1, joinm);
                    });
                }
            }
            if (this.group.Permission >= Server.adminchatperm && Server.adminsjoinsilent == true)
            {
                this.hidden = true;
                this.adminchat = true;
            }

            if (Server.verifyadmins)
            {
                if (this.group.Permission >= Server.verifyadminsrank)
                {
                    if (!Directory.Exists("extra/passwords") || !File.Exists("extra/passwords/" + this.name + ".dat"))
                    {
                        this.SendMessage("&cPlease set your admin verification password with &a/setpass [Password]!");
                    }
                    else
                    {
                        this.SendMessage("&cPlease complete admin verification with &a/pass [Password]!");
                    }
                }
            }
            try
            {
                Waypoint.Load(this);
                //if (Waypoints.Count > 0) { this.SendMessage("Loaded " + Waypoints.Count + " waypoints!"); }
            }
            catch (Exception ex)
            {
                SendMessage("Error loading waypoints!");
                Server.ErrorLog(ex);
            }
            try
            {
                if (File.Exists("ranks/muted.txt"))
                {
                    using (StreamReader read = new StreamReader("ranks/muted.txt"))
                    {
                        string line;
                        while ((line = read.ReadLine()) != null)
                        {
                            if (line.ToLower() == this.name.ToLower())
                            {
                                this.muted = true;
                                Player.SendMessage(this, "!%cYou are still %8muted%c since your last login.");
                                break;
                            }
                        }
                    }
                }
                else { File.Create("ranks/muted.txt").Close(); }
            }
            catch { muted = false; }
            if (!UsingID)
            {
                Server.s.Log(name + " [" + ip + "] + has joined the server.");
            }
            else
            {
                Server.s.Log(name + " [" + ip + "]" + "(" + ID + ") + has joined the server.");
            }

            if (Server.zombie.GameInProgess())
            {
                if (level.name == Server.zombie.currentLevelName)
                    Server.zombie.InfectedPlayerLogin(this);
            }
        }

        public void SetPrefix()
        { //just change the color name if someone ever decides these titles need different colors O.o I just try to match them with the ranks on mcforge.org
            string viptitle = isOldDev ? string.Format("{1}[{0}Old Dev{1}] ", c.Parse("blue"), color) : isMod ? string.Format("{1}[{0}Mod{1}] ", c.Parse("lime"), color) : isGCMod ? string.Format("{1}[{0}GCMod{1}] ", c.Parse("gold"), color) : "";
            prefix = (title == "") ? "" : (titlecolor == "") ? color + "[" + title + "] " : color + "[" + titlecolor + title + color + "] ";
            prefix = viptitle + prefix;
        }

        void HandleBlockchange(byte[] message)
        {
            int section = 0;
            try
            {
                //byte[] message = (byte[])m;
                if (!loggedIn)
                    return;
                if (CheckBlockSpam())
                    return;

                section++;
                ushort x = NTHO(message, 0);
                ushort y = NTHO(message, 2);
                ushort z = NTHO(message, 4);
                byte action = message[6];
                ushort type = message[7];

                if (action == 1 && Server.ZombieModeOn && Server.noPillaring)
                {
                    if (!referee)
                    {
                        if (lastYblock == y - 1 && lastXblock == x && lastZblock == z)
                        {
                            blocksStacked++;
                        }
                        else
                        {
                            blocksStacked = 0;
                        }
                        if (blocksStacked == 2)
                        {
                            SendMessage("You are pillaring! Stop before you get kicked!");
                        }
                        if (blocksStacked == 4)
                        {
                            Command.all.Find("kick").Use(null, name + " No pillaring allowed!");
                        }
                    }
                }

                lastYblock = y;
                lastXblock = x;
                lastZblock = z;

                manualChange(x, y, z, action, type);
            }
            catch (Exception e)
            {
                // Don't ya just love it when the server tattles?
                GlobalMessageOps(name + " has triggered a block change error");
                GlobalMessageOps(e.GetType().ToString() + ": " + e.Message);
                Server.ErrorLog(e);
            }
        }

        /*     public void MakeExplosion(string name, ushort x, ushort y, ushort z, int size, bool bigtnt, bool nuke, string levelname, ushort b, bool tnt)
             {
                 for (int xt = 0; xt <= 100; xt++)
                 {
                     Thread.Sleep(10);
                 }
                 Level level = Level.Find(levelname);*/
        /*foreach (Player ppp in Player.players)
        {
            ppp.killingPeople = false;
            ppp.amountKilled = 0;
            Server.killed.Clear();
        }
        level.MakeExplosion(name, x, y, z, 0, bigtnt, nuke, tnt);
        Player that = Player.Find(name);
        that.SendBlockchange(x, y, z, b);
    }*/

        /* public void MakeInstantExplosion(string name, ushort x, ushort y, ushort z, int size, bool bigtnt, bool nuke, string levelname, ushort b)
         {
             Level level = Level.Find(levelname);
             /*foreach (Player ppp in Player.players)
             {
                 ppp.killingPeople = false;
                 ppp.amountKilled = 0;
                 Server.killed.Clear();
             }*/
        /*  level.MakeExplosion(name, x, y, z, 0, bigtnt, nuke, true);
          Player that = Player.Find(name);
          that.SendBlockchange(x, y, z, b);
      }
      */
        /*    public void MakeLightningsplosion(string name, ushort x, ushort y, ushort z, int size, string levelname, ushort b)
            {
                for (int xt = 0; xt <= 3; xt++)
                {
                    Thread.Sleep(50);
                }
                Level level = Level.Find(levelname);
                /*foreach (Player ppp in Player.players)
                {
                    ppp.killingPeople = false;
                    ppp.amountKilled = 0;
                    Server.killed.Clear();
                }*/
        /*      level.makeLightningsplosion(name, x, y, z, 0);
              try
              {
                  if (lightningUpgrade > 0)
                  {
                      level.makeLightningsplosion(name, (ushort)(x + 1), y, z, 0);
                      level.makeLightningsplosion(name, (ushort)(x - 1), y, z, 0);
                      level.makeLightningsplosion(name, x, y, (ushort)(z + 1), 0);
                      level.makeLightningsplosion(name, x, y, (ushort)(z - 1), 0);
                  }
              }
              catch { }

              if (lightningUpgrade > 1)
              {
                  Random rand = new Random();
                  int random = rand.Next(0, 49);
                  int random1 = rand.Next(0, 49);
                  if (random == random1)
                  {
                      var randomlyOrdered = Player.players.OrderBy(i => rand.Next());
                      foreach (var i in randomlyOrdered)
                      {
                          level.makeLightningsplosion(name, (ushort)(i.pos[0] / 32), (ushort)(i.pos[1] / 32), (ushort)(i.pos[2] / 32), 0);
                          break;
                      }
                  }
              }
              Player that = Player.Find(name);
              that.SendBlockchange(x, y, z, b);
          }


          public void MakeUpsideDownLightningsplosion(string name, ushort x, ushort y, ushort z, int size, string levelname, ushort b)
          {
              for (int xt = 0; xt <= 3; xt++)
              {
                  Thread.Sleep(50);
              }
              Level level = Level.Find(levelname);
              /*foreach (Player ppp in Player.players)
              {
                  ppp.killingPeople = false;
                  ppp.amountKilled = 0;
                  Server.killed.Clear();
              }*/
        /*  level.makeUpsideDownLightning(name, x, y, z, 0);

          Player that = Player.Find(name);
          that.SendBlockchange(x, y, z, b);
      }
  */
        /*public void MakeLinesplosion(string name, ushort x, ushort y, ushort z, int size, bool lazer, string levelname, ushort b)
        {
            for (int xt = 0; xt <= 3; xt++)
            {
                Thread.Sleep(50);
            }
            Level level = Level.Find(levelname);

            try
            {
                if (lazerUpgrade > 0 && lazer)
                    level.makeLinesplosion(name, (ushort)(x + 1), y, (ushort)(z + 1), 0, lazer);
            }
            catch { }
            level.makeLinesplosion(name, x, y, z, 0, lazer);

            if (lazerUpgrade > 2 && lazer && !lazerTimer.Enabled)
            {
                lazerPos = new ushort[] { x, y, z };
                lazerTimer = new System.Timers.Timer(1000 * 2); ///Ztime timer
                lazerTimer.Elapsed += new ElapsedEventHandler(SecondLazer);
                lazerTimer.Enabled = true;
            }
            Player that = Player.Find(name);
            that.SendBlockchange(x, y, z, b);
        }

        public void SecondLazer(object sender, ElapsedEventArgs e)
        {
            lazerTimer.Enabled = false;
            Level level = this.level;
            ushort x = lazerPos[0];
            ushort y = lazerPos[1];
            ushort z = lazerPos[2];
            try
            {
                level.makeLinesplosion(name, (ushort)(x + 1), y, (ushort)(z + 1), 0, true);
            }
            catch { }
            level.makeLinesplosion(name, x, y, z, 0, true);
            lazerTimer.Stop();
        }

        public void MakeFreezeRay(string name, ushort x, ushort y, ushort z, int size, bool lazer, string levelname, ushort b)
        {
            for (int xt = 0; xt <= 3; xt++)
            {
                Thread.Sleep(50);
            }
            Level level = Level.Find(levelname);

            try
            {
                if (lazerUpgrade > 0 && lazer)
                    level.makeFreezeRay(name, (ushort)(x + 1), y, (ushort)(z + 1), 0);
            }
            catch { }
            level.makeFreezeRay(name, x, y, z, 0);

            Player that = Player.Find(name);
            that.SendBlockchange(x, y, z, b);
        }*/

        /* public void freezePlayer(ushort x, ushort y, ushort z)
         {
             foreach (Player ppp in Player.players)
             {
                 if (ppp.pos[0] / 32 == x && !ppp.invinciblee)
                     if ((ppp.pos[1] / 32 == y) || ((ppp.pos[1] / 32) - 1 == y) || ((ppp.pos[1] / 32) + 1 == y))
                         if (ppp.pos[2] / 32 == z)
                         {
                             if (!Server.killed.Contains(ppp) && !ppp.deathTimerOn && !this.referee && !ppp.referee && !Server.pctf.InSpawn(ppp, ppp.pos) && ppp != this && (Server.pctf.getTeam(this) != Server.pctf.getTeam(ppp)))
                             {
                                 ppp.freezeTimer = new System.Timers.Timer(1000 * 2); ///Ztime timer
                                 if (!ppp.freezeTimer.Enabled)
                                 {
                                     ppp.SendMessage(c.gray + " - " + Server.DefaultColor + "You were frozen by " + this.name + "'s freeze ray!" + c.gray + " - ");
                                     SendMessage(c.gray + " - " + Server.DefaultColor + "You froze " + ppp.name + "!" + c.gray + " - ");
                                     ppp.hasBeenTrapped = true;
                                     ppp.freezeTimer.Elapsed += new ElapsedEventHandler(ppp.unFreezePlayer);
                                     ppp.freezeTimer.Enabled = true;
                                 }
                             }
                         }
             }
         }

         public void unFreezePlayer(object sender, ElapsedEventArgs e)
         {
             hasBeenTrapped = false;
             freezeTimer.Enabled = false;
             freezeTimer.Stop();
         }


         public void MakeLine(string name, ushort x, ushort y, ushort z, int size, bool lazer, string levelname, ushort b)
         {
             for (int xt = 0; xt <= 3; xt++)
             {
                 Thread.Sleep(50);
             }
             Level level = Level.Find(levelname);
             /*foreach (Player ppp in Player.players)
             {
                 ppp.killingPeople = false;
                 ppp.amountKilled = 0;
                 Server.killed.Clear();
             }*/
        /*  level.makeLine(name, x, y, z, 0, lazer);
          Player that = Player.Find(name);
          that.SendBlockchange(x, y, z, b);
      }*/

        public void manualChange(ushort x, ushort y, ushort z, byte action, ushort type)
        {
            if (type > 1024)
            {
                Kick("Unknown block type!");
                return;
            }

            ushort b = level.GetTile(x, y, z);
            /*if (type != 0 && type <= 65 && Server.SMPMode && InSMP && inventory.Remove((byte)type, 1) == false)
            {
                SendMessage("You do not have this block.");
                SendBlockchange(x, y, z, b);
                return;
            }
            if (type == 0 && Server.SMPMode && InSMP)
            {
                inventory.Add((byte)type, 1);
                return;
            }*/
            if (b == Block.Zero) { return; }
            if (jailed || !agreed) { SendBlockchange(x, y, z, b); return; }
            if (level.name.Contains("Museum " + Server.DefaultColor) && Blockchange == null)
            {
                return;
            }

            if (!deleteMode)
            {
                string info = level.foundInfo(x, y, z);
                if (info.Contains("wait")) { return; }
            }

            if (!canBuild)
            {
                SendBlockchange(x, y, z, b);
                return;
            }

            if (Server.verifyadmins == true)
            {
                if (this.adminpen == true)
                {
                    SendBlockchange(x, y, z, b);
                    this.SendMessage("&cYou must use &a/pass [Password]&c to verify!");
                    return;
                }
            }

            if (Server.ZombieModeOn && (action == 1 || (action == 0 && this.painting)))
            {
                if (Server.zombie != null && this.level.name == Server.zombie.currentLevelName && Server.limitedblocks)
                {
                    if (blockCount == 0)
                    {
                        if (!referee)
                        {
                            SendMessage("You have no blocks left.");
                            SendBlockchange(x, y, z, b); return;
                        }
                    }

                    if (!referee)
                    {
                        blockCount--;
                        if (blockCount == 40 || blockCount == 30 || blockCount == 20 || blockCount <= 10 && blockCount >= 0)
                        {
                            SendMessage("Blocks Left: " + c.maroon + blockCount + Server.DefaultColor);
                        }
                    }
                }
            }

            if (Server.lava.active)
            {

                if (Server.lava.HasPlayer(this) && Server.lava.IsPlayerDead(this))
                {
                    SendMessage("You are out of the round, and cannot build.");
                    SendBlockchange(x, y, z, b);
                    return;
                }

                if (this.spongesLeft == 0 && type == Block.sponge && (action == 1 || (action == 0 && this.painting)))
                {
                    SendMessage(c.green + "You need to purchase more sponges at the /store!");
                    SendBlockchange(x, y, z, b); return;
                }
                else if (this.spongesLeft >= 0 && type == Block.sponge && (action == 1 || (action == 0 && this.painting)))
                {
                    this.spongesLeft = this.spongesLeft - 1;
                    Player.SendMessage(this, c.lime + "You have " + c.red + spongesLeft + c.lime + " sponges left!");
                    type = Block.lava_sponge;
                }
            }

            Blockchange bP = new Blockchange();
            bP.username = name;
            bP.level = level.name;
            bP.timePerformed = DateTime.Now;
            bP.x = x; bP.y = y; bP.z = z;
            bP.type = type;

            lastClick[0] = x;
            lastClick[1] = y;
            lastClick[2] = z;
            //bool test2 = false;
            if (Blockchange != null)
            {
                if (Blockchange.Method.ToString().IndexOf("AboutBlockchange") == -1 && !level.name.Contains("Museum " + Server.DefaultColor))
                {
                    bP.deleted = true;
                    level.blockCache.Add(bP);
                }

                Blockchange(this, x, y, z, type);
                return;
            }
            if (PlayerBlockChange != null)
                PlayerBlockChange(this, x, y, z, type);
            OnBlockChangeEvent.Call(this, x, y, z, type);
            if (cancelBlock)
            {
                cancelBlock = false;
                return;
            }

            if (group.Permission == LevelPermission.Banned) return;
            if (group.Permission == LevelPermission.Guest)
            {
                int Diff = 0;

                Diff = Math.Abs((int)(pos[0] / 32) - x);
                Diff += Math.Abs((int)(pos[1] / 32) - y);
                Diff += Math.Abs((int)(pos[2] / 32) - z);

                if (Diff > 12)
                {
                    if (lastCMD != "click")
                    {
                        Server.s.Log(name + " attempted to build with a " + Diff.ToString() + " distance offset");
                        GlobalMessageOps("To Ops &f-" + color + name + "&f- attempted to build with a " + Diff.ToString() + " distance offset");
                        SendMessage("You can't build that far away.");
                        SendBlockchange(x, y, z, b); return;
                    }
                }

                if (Server.antiTunnel)
                {
                    if (!ignoreGrief && !PlayingTntWars)
                    {
                        if (y < level.depth / 2 - Server.maxDepth)
                        {
                            SendMessage("You're not allowed to build this far down!");
                            SendBlockchange(x, y, z, b); return;
                        }
                    }
                }
            }

            if (b == Block.griefer_stone && group.Permission <= Server.grieferStoneRank && !isOldDev && !isMod)
            {
                if (grieferStoneWarn < 1)
                    SendMessage("Do not grief! This is your first warning!");
                else if (grieferStoneWarn < 2)
                    SendMessage("Do NOT grief! Next time you will be " + (Server.grieferStoneBan ? "banned for 30 minutes" : "kicked") + "!");
                else
                {
                    if (Server.grieferStoneBan)
                        try { Command.all.Find("tempban").Use(null, name + " 30"); }
                        catch (Exception ex) { Server.ErrorLog(ex); }
                    else
                        Kick(Server.customGrieferStone ? Server.customGrieferStoneMessage : "Oh noes! You were caught griefing!");
                    return;
                }
                grieferStoneWarn++;
                SendBlockchange(x, y, z, b);
                return;
            }
            if (!Block.canPlace(this, b) && !Block.BuildIn(b) && !Block.AllowBreak(b))
            {
                SendMessage("Cannot build here!");
                SendBlockchange(x, y, z, b);
                return;
            }

            if (!Block.canPlace(this, type))
            {
                SendMessage("You can't place this block type!");
                SendBlockchange(x, y, z, b);
                return;
            }

            if (b >= 200 && b < 220)
            {
                SendMessage("Block is active, you cant disturb it!");
                SendBlockchange(x, y, z, b);
                return;
            }

            /*   if (level.name == Server.pctf.currentLevelName && Server.CTFModeOn && Server.ctfRound)
               {
                   if (type == Block.tnt)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (autoTNT)
                       {
                           if (action == 1)
                           {
                               DateTime dft = DateTime.Now;
                               int sec = (dft.Hour * 60 * 60) + (dft.Minute * 60) + dft.Second;
                               if (sec >= (tntSeconds + 2))
                               {
                                   if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                                   if (tntUpgrade < 3 || Block.BuildIn(b))
                                       SendBlockchange(x, y, z, Block.tnt);
                                   DateTime dt = DateTime.Now;
                                   tntSeconds = (dt.Hour * 60 * 60) + (dt.Minute * 60) + dt.Second;
                                   bool bigtntt = false;
                                   if (bigtnt > 0) { bigtntt = true; bigtnt = bigtnt - 1; }
                                   MakeExplosion(this.name, x, y, z, 0, bigtntt, false, level.name, b, true);
                                   return;
                               }
                               else
                               {
                                   SendBlockchange(x, y, z, b);
                                   return;
                               }
                           }
                       }
                       else
                       {
                           if (action == 1 && tntPlaced == 0)
                           {
                               if (tntUpgrade < 3 || Block.BuildIn(b))
                                   SendBlockchange(x, y, z, Block.tnt);
                               DateTime dft = DateTime.Now;
                               int sec = (dft.Hour * 60 * 60) + (dft.Minute * 60) + dft.Second;
                               {
                                   tntPlaced = tntPlaced + 1;
                                   tntPlacement[0] = x; tntPlacement[1] = y; tntPlacement[2] = z;
                                   return;
                               }
                           }
                           else if (action == 0)
                           {
                               Player.players.ForEach(delegate(Player player)
                               {
                                   if (player.tntPlaced >= 1)
                                   {
                                       if (x == player.tntPlacement[0] && z == player.tntPlacement[2] && y == player.tntPlacement[1])
                                       {
                                           player.tntPlacement[0] = 0; player.tntPlacement[1] = 0; player.tntPlacement[2] = 0;
                                           player.tntPlaced = player.tntPlaced - 1;
                                       }
                                   }
                               });
                           }
                       }
                   }


                   if (type == Block.darkgrey || b == Block.mine)
                   {
                       if (action == 1)
                       {
                           if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                           if (minesPlaced == 1) { SendBlockchange(x, y, z, Block.air); return; }
                           this.minesPlaced = this.minesPlaced + 1;
                           minePlacement[0] = Convert.ToUInt16((int)x * 32); minePlacement[1] = Convert.ToUInt16((int)y * 32); minePlacement[2] = Convert.ToUInt16((int)z * 32);
                           SendMessage("Your mine has been placed, arming it in 2 seconds");
                           Thread.Sleep(2000);
                           Player.SendMessage(this, c.gray + " - " + Server.DefaultColor + "Your mine has been armed!" + c.gray + " - ");
                           bP.deleted = false;
                           bP.type = Block.mine;
                           level.blockCache.Add(bP);
                           placeBlock(b, Block.mine, x, y, z);
                           return;
                       }
                       else if (action == 0)
                       {
                           Player.players.ForEach(delegate(Player player)
                           {
                               if (player.minesPlaced >= 1)
                               {
                                   if (x == (player.minePlacement[0] / 32) && z == (player.minePlacement[2] / 32) && (y == (player.minePlacement[1] / 32) || y == (player.minePlacement[1] / 32)))
                                   {
                                       GlobalMessage(player.color + player.name + "'s &0mine has been disarmed by " + this.color + this.name);
                                       player.SendMessage(c.gray + " - " + Server.DefaultColor + "Your mine has been disarmed!" + c.gray + " - ");
                                       player.minePlacement[0] = 0; player.minePlacement[1] = 0; player.minePlacement[2] = 0;
                                       player.minesPlaced = player.minesPlaced - 1;
                                   }
                               }
                           });
                       }
                   }

                   if (type == Block.dirt)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 1 && !PlacedNukeThisRound)
                       {
                           DateTime dft = DateTime.Now;
                           int sec = (dft.Hour * 60 * 60) + (dft.Minute * 60) + dft.Second;
                           if (sec >= (tntSeconds + 2))
                           {
                               if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, Block.air); return; }
                               if (nuke < 1) { return; }
                               PlacedNukeThisRound = true;
                               SendBlockchange(x, y, z, Block.tnt);
                               DateTime dt = DateTime.Now;
                               tntSeconds = (dt.Hour * 60 * 60) + (dt.Minute * 60) + dt.Second;
                               MakeExplosion(this.name, x, y, z, 0, false, true, level.name, b, false);
                               return;
                           }
                           else
                           {
                               SendBlockchange(x, y, z, b);
                               return;
                           }
                       }
                   }

                   if (type == Block.purple)
                   {
                       if (action == 1 && tntPlaced > 0)
                       {
                           if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                           DateTime dt = DateTime.Now;
                           tntSeconds = (dt.Hour * 60 * 60) + (dt.Minute * 60) + dt.Second;
                           bool bigtntt = false;
                           if (bigtnt > 0) { bigtntt = true; bigtnt = bigtnt - 1; }
                           MakeInstantExplosion(this.name, tntPlacement[0], tntPlacement[1], tntPlacement[2], 0, bigtntt, false, level.name, b);
                           tntPlaced = tntPlaced - 1;
                           tntPlacement[0] = 0; tntPlacement[1] = 0; tntPlacement[2] = 0;
                           SendBlockchange(x, y, z, b);
                           return;
                       }
                   }

                   if (type == Block.mushroom || b == Block.trap)
                   {
                       if (action == 1)
                       {
                           if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                           if (trapsPlaced == 1) return;
                           if (traps < 1) { SendMessage("&8 - " + Server.DefaultColor + "You need to buy trap at the /store!" + "&8 - "); SendBlockchange(x, y, z, b);  return; }
                           traps = traps - 1;
                           Thread.Sleep(500);
                           this.trapsPlaced = this.trapsPlaced + 1;
                           trapPlacement[0] = Convert.ToUInt16((int)x * 32); trapPlacement[1] = Convert.ToUInt16((int)y * 32); trapPlacement[2] = Convert.ToUInt16((int)z * 32);
                           Player.SendMessage(this, c.gray + " - " + Server.DefaultColor + "Your trap has been armed!" + c.gray + " - ");
                           bP.deleted = false;
                           bP.type = Block.trap;
                           level.blockCache.Add(bP);
                           placeBlock(b, Block.trap, x, y, z);
                           return;
                       }
                       else if (action == 0)
                       {
                           Player.players.ForEach(delegate(Player player)
                           {
                               player.hasBeenTrapped = false;
                               if (player.trapsPlaced >= 1)
                               {
                                   if (x == (player.trapPlacement[0] / 32) && z == (player.trapPlacement[2] / 32) && (y == (player.trapPlacement[1] / 32) || y == (player.trapPlacement[1] / 32)))
                                   {
                                       player.SendMessage(c.gray + " - " + Server.DefaultColor + "Your trap has been disarmed!" + c.gray + " - ");
                                       player.trapPlacement[0] = 0; player.trapPlacement[1] = 0; player.trapPlacement[2] = 0;
                                       player.trapsPlaced = player.trapsPlaced - 1;
                                   }
                               }
                           });
                       }
                   }

                   if (type == Block.gravel)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 1)
                       {
                           DateTime dft = DateTime.Now;
                           int sec = (dft.Hour * 60 * 60) + (dft.Minute * 60) + dft.Second;
                           if (sec >= (lazorSeconds + 1))
                           {
                               DateTime dt = DateTime.Now;
                               lazorSeconds = (dt.Hour * 60 * 60) + (dt.Minute * 60) + dt.Second;
                               MakeLinesplosion(this.name, x, y, z, 0, false, level.name, b);
                               return;
                           }
                           else
                           {
                               SendBlockchange(x, y, z, b);
                               return;
                           }
                       }
                   }

                   if (type == Block.white)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 1)
                       {
                           if (jetpack < 1) { SendMessage("&8 - " + Server.DefaultColor + "You need to buy jetpack at the /store!" + "&8 - "); SendBlockchange(x, y, z, b); return; }
                           jetpack = jetpack - 1;
                           Command.all.Find("slap").Use(null, this.name);
                           SendBlockchange(x, y, z, b);
                           return;
                       }
                   }

                   if (type == Block.sand)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 1)
                       {
                           DateTime dft = DateTime.Now;
                           int sec = (dft.Hour * 60 * 60) + (dft.Minute * 60) + dft.Second;
                           if (sec >= (lazorSeconds + 1))
                           {
                               if (lazers < 1) { SendMessage("&8 - " + Server.DefaultColor + "You need to buy lazers at the /store!" + "&8 - "); SendBlockchange(x, y, z, b); return; }
                               lazers = lazers - 1;
                               DateTime dt = DateTime.Now;
                               lazorSeconds = (dt.Hour * 60 * 60) + (dt.Minute * 60) + dt.Second;
                               MakeLinesplosion(this.name, x, y, z, 0, true, level.name, b);
                               return;
                           }
                           else
                           {
                               SendBlockchange(x, y, z, b);
                               return;
                           }
                       }
                   }

                   if (type == Block.blue)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 1)
                       {
                           DateTime dft = DateTime.Now;
                           int sec = (dft.Hour * 60 * 60) + (dft.Minute * 60) + dft.Second;
                           if (sec >= (lazorSeconds + 1))
                           {
                               if (freeze < 1) { SendMessage("&8 - " + Server.DefaultColor + "You need to buy freezerays at the /store!" + "&8 - "); SendBlockchange(x, y, z, b); return; }
                               freeze = freeze - 1;
                               DateTime dt = DateTime.Now;
                               lazorSeconds = (dt.Hour * 60 * 60) + (dt.Minute * 60) + dt.Second;
                               MakeFreezeRay(this.name, x, y, z, 0, true, level.name, b);
                               return;
                           }
                           else
                           {
                               SendBlockchange(x, y, z, b);
                               return;
                           }
                       }
                   }

                   if (type == Block.coal)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 1)
                       {
                           DateTime dft = DateTime.Now;
                           int sec = (dft.Hour * 60 * 60) + (dft.Minute * 60) + dft.Second;
                           if (sec >= (lazorSeconds + 1))
                           {
                               if (lines < 1) { SendMessage("&8 - " + Server.DefaultColor + "You need to buy line at the /store!" + "&8 - "); SendBlockchange(x, y, z, b); return; }

                               lines = lines - 1;
                               DateTime dt = DateTime.Now;
                               lazorSeconds = (dt.Hour * 60 * 60) + (dt.Minute * 60) + dt.Second;
                               MakeLine(this.name, x, y, z, 0, false, level.name, b);
                               return;
                           }
                           else
                           {
                               SendBlockchange(x, y, z, b);
                               return;
                           }
                       }
                   }

                   if (type == Block.lightblue)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 1)
                       {
                           DateTime dft = DateTime.Now;
                           int sec = (dft.Hour * 60 * 60) + (dft.Minute * 60) + dft.Second;
                           if (sec >= (lightSeconds + 2))
                           {
                               if (lightnings < 1) { SendMessage("&8 - " + Server.DefaultColor + "You need to buy lightning at the /store!" + "&8 - "); SendBlockchange(x, y, z, b); return; }
                               lightnings = lightnings - 1;
                               DateTime dt = DateTime.Now;
                               lightSeconds = (dt.Hour * 60 * 60) + (dt.Minute * 60) + dt.Second;
                               MakeLightningsplosion(this.name, x, y, z, 0, level.name, b);
                               return;
                           }
                           else
                           {
                               SendBlockchange(x, y, z, b);
                               return;
                           }
                       }
                   }

                   if (type == Block.brick)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 1)
                       {
                           if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                           if (tripwiresPlaced == 1) return;
                           if (tripwire < 1) { SendMessage("&8 - " + Server.DefaultColor + "You need to buy tripwire at the /store!" + "&8 - "); SendBlockchange(x, y, z, b); return; }
                           tripwire = tripwire - 1;
                           Thread.Sleep(500);
                           this.tripwiresPlaced = this.tripwiresPlaced + 1;

                           ushort x1 = x;
                           ushort y1 = y;
                           ushort z1 = z;

                           tripwire1[0] = Convert.ToUInt16((int)x * 32); tripwire1[1] = Convert.ToUInt16((int)y * 32); tripwire1[2] = Convert.ToUInt16((int)z * 32);

                           int rot360 = (int)Math.Round(rot[0] * 1.40625f);

                           Player.SendMessage(this, c.gray + " - " + Server.DefaultColor + "Your tripwire has been armed!" + c.gray + " - ");
                           bP.deleted = false;
                           bP.type = Block.brick;
                           level.blockCache.Add(bP);
                           placeBlock(b, Block.brick, x, y, z);
                           return;
                       }
                       else if (action == 0)
                       {
                           Player.players.ForEach(delegate(Player player)
                           {
                               player.hasBeenTrapped = false;
                               if (player.tripwiresPlaced >= 1)
                               {
                                   if ((x == (player.tripwire1[0] / 32) && z == (player.tripwire1[2] / 32) && y == (player.tripwire1[1] / 32)) ||
                                       (x == (player.tripwire2[0] / 32) && z == (player.tripwire2[2] / 32) && y == (player.tripwire2[1] / 32)))
                                   {
                                       player.SendMessage(c.gray + " - " + Server.DefaultColor + "Your trap has been disarmed!" + c.gray + " - ");
                                       player.trapPlacement[0] = 0; player.trapPlacement[1] = 0; player.trapPlacement[2] = 0;
                                       player.trapsPlaced = player.trapsPlaced - 1;
                                   }
                               }
                           });
                       }
                   }

                   if (type == Block.green)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 1)
                       {
                           DateTime dft = DateTime.Now;
                           int sec = (dft.Hour * 60 * 60) + (dft.Minute * 60) + dft.Second;
                           if (sec >= (lightSeconds + 2))
                           {
                               DateTime dt = DateTime.Now;
                               lightSeconds = (dt.Hour * 60 * 60) + (dt.Minute * 60) + dt.Second;
                               MakeUpsideDownLightningsplosion(this.name, x, y, z, 0, level.name, b);
                               return;
                           }
                           else
                           {
                               SendBlockchange(x, y, z, b);
                               return;
                           }
                       }
                   }

                   if (b == Block.blueflag && Server.ctfRound)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 0)
                       {
                           bool lol = Server.pctf.grabFlag(this, "blue", x, y, z);
                           if (lol)
                           {
                               SendBlockchange(x, y, z, b);
                               return;
                           }
                       }
                   }

                   if (b == Block.redflag && Server.ctfRound)
                   {
                       if (Server.pctf.getTeam(this) == null) { SendBlockchange(x, y, z, b); return; }
                       if (action == 0)
                       {
                           bool lol2 = Server.pctf.grabFlag(this, "red", x, y, z);
                           if (lol2)
                           {
                               SendBlockchange(x, y, z, b);
                               return;
                           }
                       }
                   }
               }*/

            if (action > 1) { Kick("Unknown block action!"); }

            ushort oldType = type;
            type = bindings[(int)type];
            //Ignores updating blocks that are the same and send block only to the player
            if (b == (byte)((painting || action == 1) ? type : (byte)0))
            {
                if (painting || oldType != type) { SendBlockchange(x, y, z, b); }
                return;
            }
            //else

            if (!painting && action == 0)
            {
                if (!deleteMode)
                {
                    if (Block.portal(b)) { HandlePortal(this, x, y, z, b); return; }
                    if (Block.mb(b)) { HandleMsgBlock(this, x, y, z, b); return; }
                }

                bP.deleted = true;
                level.blockCache.Add(bP);
                deleteBlock(b, type, x, y, z);
            }
            else
            {
                bP.deleted = false;
                level.blockCache.Add(bP);
                placeBlock(b, type, x, y, z);
            }
        }

        public void createTntAnimation(ushort[] start, out List<ushort[]> animation)
        {
            animation = new List<ushort[]>();
            for (int i = -1; i <= 1; i++)
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                    {
                        animation.Add(new[] { (ushort)(start[0] - i), (ushort)(start[1] - x), (ushort)(start[2] - y) });
                    }
            animation.Remove(start);
        }

        public void HandlePortal(Player p, ushort x, ushort y, ushort z, ushort b)
        {
            try
            {
                foreach (Portal po in PortalDB.portals)
                {
                    if (po.entrance.ToLower() == p.level.name.ToLower() && po.x1 == x && po.y1 == y && po.z1 == z)
                    {
                        if (po.entrance != po.exit)
                        {
                            ignorePermission = true;
                            Command.all.Find("goto").Use(p, po.exit);
                            ignorePermission = false;
                        }

                        Player.GlobalMessage(color + name + Server.DefaultColor + " used the portal &a" + po.name);

                        Command.all.Find("move").Use(p, p.name + " " + po.x2 + " " + po.y2 + " " + po.z2);
                        Thread.Sleep(1000);
                    }
                }
            }
            catch { Player.SendMessage(p, "Portal had no exit."); return; }
        }


        public void HandleMsgBlock(Player p, ushort x, ushort y, ushort z, ushort b)
        {
            try
            {
                int foundMessages = 0;

                foreach (MessageBlock mb in MessageBlockDB.messageBlocks)
                {
                    if (mb.level == level.name && mb.x == x && mb.y == y && mb.z == z)
                    {
                        foundMessages++;

                        if (mb.message != prevMsg || Server.repeatMessage)
                        {
                            if (mb.message.StartsWith("/"))
                            {
                                mb.message = mb.message.Remove(0, 1);
                                int pos = mb.message.IndexOf(' ');
                                string cmd = mb.message.Trim();
                                string args = "";
                                try
                                {
                                    cmd = mb.message.Substring(0, pos);
                                    args = mb.message.Substring(pos + 1);
                                }
                                catch { }
                                HandleCommand(cmd, args);
                            }
                            else
                            {
                                Player.SendMessage(p, mb.message);
                            }
                            prevMsg = mb.message;
                        }

                        SendBlockchange(x, y, z, b);
                    }
                }

                if (foundMessages < 1)
                {
                    level.Blockchange(this, x, y, z, Block.air);
                }
            }
            catch { Player.SendMessage(p, "No message was stored."); return; }
        }


        private bool checkOp()
        {
            return group.Permission < LevelPermission.Operator;
        }

        private void deleteBlock(ushort b, ushort type, ushort x, ushort y, ushort z)
        {
            Random rand = new Random();
            int mx, mz;

            if (deleteMode && b != Block.c4det) { level.Blockchange(this, x, y, z, Block.air); return; }

            if (Block.tDoor(b)) { SendBlockchange(x, y, z, b); return; }
            if (Block.DoorAirs(b) != 0)
            {
                if (level.physics != 0) level.Blockchange(x, y, z, Block.DoorAirs(b));
                else SendBlockchange(x, y, z, b);
                return;
            }
            if (Block.odoor(b) != Block.Zero)
            {
                if (b == Block.odoor8 || b == Block.odoor8_air)
                {
                    level.Blockchange(this, x, y, z, Block.odoor(b));
                }
                else
                {
                    SendBlockchange(x, y, z, b);
                }
                return;
            }

            switch (b)
            {
                case Block.door_air: //Door_air
                case Block.door2_air:
                case Block.door3_air:
                case Block.door4_air:
                case Block.door5_air:
                case Block.door6_air:
                case Block.door7_air:
                case Block.door8_air:
                case Block.door9_air:
                case Block.door10_air:
                case Block.door_iron_air:
                case Block.door_gold_air:
                case Block.door_cobblestone_air:
                case Block.door_red_air:

                case Block.door_dirt_air:
                case Block.door_grass_air:
                case Block.door_deepblue_air:
                case Block.door_book_air:
                    break;
                case Block.rocketstart:
                    if (level.physics < 2 || level.physics == 5)
                    {
                        SendBlockchange(x, y, z, b);
                    }
                    else
                    {
                        int newZ = 0, newX = 0, newY = 0;

                        SendBlockchange(x, y, z, Block.rocketstart);
                        if (rot[0] < 48 || rot[0] > (256 - 48))
                            newZ = -1;
                        else if (rot[0] > (128 - 48) && rot[0] < (128 + 48))
                            newZ = 1;

                        if (rot[0] > (64 - 48) && rot[0] < (64 + 48))
                            newX = 1;
                        else if (rot[0] > (192 - 48) && rot[0] < (192 + 48))
                            newX = -1;

                        if (rot[1] >= 192 && rot[1] <= (192 + 32))
                            newY = 1;
                        else if (rot[1] <= 64 && rot[1] >= 32)
                            newY = -1;

                        if (192 <= rot[1] && rot[1] <= 196 || 60 <= rot[1] && rot[1] <= 64) { newX = 0; newZ = 0; }

                        ushort b1 = level.GetTile((ushort)(x + newX * 2), (ushort)(y + newY * 2), (ushort)(z + newZ * 2));
                        ushort b2 = level.GetTile((ushort)(x + newX), (ushort)(y + newY), (ushort)(z + newZ));
                        if (b1 == Block.air && b2 == Block.air && level.CheckClear((ushort)(x + newX * 2), (ushort)(y + newY * 2), (ushort)(z + newZ * 2)) && level.CheckClear((ushort)(x + newX), (ushort)(y + newY), (ushort)(z + newZ)))
                        {
                            level.Blockchange((ushort)(x + newX * 2), (ushort)(y + newY * 2), (ushort)(z + newZ * 2), Block.rockethead);
                            level.Blockchange((ushort)(x + newX), (ushort)(y + newY), (ushort)(z + newZ), Block.fire);
                        }
                    }
                    break;
                case Block.firework:
                    if (level.physics == 5)
                    {
                        SendBlockchange(x, y, z, b);
                        return;
                    }
                    if (level.physics != 0)
                    {
                        mx = rand.Next(0, 2); mz = rand.Next(0, 2);
                        ushort b1 = level.GetTile((ushort)(x + mx - 1), (ushort)(y + 2), (ushort)(z + mz - 1));
                        ushort b2 = level.GetTile((ushort)(x + mx - 1), (ushort)(y + 1), (ushort)(z + mz - 1));
                        if (b1 == Block.air && b2 == Block.air && level.CheckClear((ushort)(x + mx - 1), (ushort)(y + 2), (ushort)(z + mz - 1)) && level.CheckClear((ushort)(x + mx - 1), (ushort)(y + 1), (ushort)(z + mz - 1)))
                        {
                            level.Blockchange((ushort)(x + mx - 1), (ushort)(y + 2), (ushort)(z + mz - 1), Block.firework);
                            level.Blockchange((ushort)(x + mx - 1), (ushort)(y + 1), (ushort)(z + mz - 1), Block.lavastill, false, "wait 1 dissipate 100");
                        }
                    }
                    SendBlockchange(x, y, z, b);

                    break;

                case Block.c4det:
                    C4.BlowUp(new ushort[] { x, y, z }, level);
                    level.Blockchange(x, y, z, Block.air);
                    break;

                default:
                    level.Blockchange(this, x, y, z, (ushort)Block.air);
                    break;
            }
            if ((level.physics == 0 || level.physics == 5) && level.GetTile(x, (ushort)(y - 1), z) == 3) level.Blockchange(this, x, (ushort)(y - 1), z, 2);
        }
        public Inventory inventory = new Inventory();
        public void placeBlock(ushort b, ushort type, ushort x, ushort y, ushort z)
        {
            if (Block.odoor(b) != Block.Zero) { SendMessage("oDoor here!"); return; }
            switch (blockAction)
            {
                case 0: //normal
                    if (level.physics == 0 || level.physics == 5)
                    {
                        switch (type)
                        {
                            case Block.dirt: //instant dirt to grass
                                if (Block.LightPass(level.GetTile(x, (ushort)(y + 1), z))) level.Blockchange(this, x, y, z, (byte)(Block.grass));
                                else level.Blockchange(this, x, y, z, (byte)(Block.dirt));
                                break;
                            case Block.staircasestep: //stair handler
                                if (level.GetTile(x, (ushort)(y - 1), z) == Block.staircasestep)
                                {
                                    SendBlockchange(x, y, z, Block.air); //send the air block back only to the user.
                                    //level.Blockchange(this, x, y, z, (byte)(null));
                                    level.Blockchange(this, x, (ushort)(y - 1), z, (byte)(Block.staircasefull));
                                    break;
                                }
                                //else
                                level.Blockchange(this, x, y, z, type);
                                break;
                            default:
                                level.Blockchange(this, x, y, z, type);
                                break;
                        }
                    }
                    else
                    {
                        level.Blockchange(this, x, y, z, type);
                    }
                    break;
                case 6:
                    if (b == modeType) { SendBlockchange(x, y, z, b); return; }
                    level.Blockchange(this, x, y, z, modeType);
                    break;
                case 13: //Small TNT
                    level.Blockchange(this, x, y, z, Block.smalltnt);
                    break;
                case 14: //Big TNT
                    level.Blockchange(this, x, y, z, Block.bigtnt);
                    break;
                case 15: //Nuke TNT
                    level.Blockchange(this, x, y, z, Block.nuketnt);
                    break;
                default:
                    Server.s.Log(name + " is breaking something");
                    blockAction = 0;
                    break;
            }
        }

        void HandleInput(object m)
        {
            if (!loggedIn || trainGrab || following != "" || frozen)
                return;
            /*if (CheckIfInsideBlock())
{
unchecked { this.SendPos((byte)-1, (ushort)(clippos[0] - 18), (ushort)(clippos[1] - 18), (ushort)(clippos[2] - 18), cliprot[0], cliprot[1]); }
return;
}*/

            byte[] message = (byte[])m;
            //      byte thisid = message[0];

            if (this.incountdown == true && CountdownGame.gamestatus == CountdownGameStatus.InProgress && CountdownGame.freezemode == true)
            {
                if (this.countdownsettemps == true)
                {
                    countdowntempx = NTHO(message, 1);
                    Thread.Sleep(100);
                    countdowntempz = NTHO(message, 5);
                    Thread.Sleep(100);
                    countdownsettemps = false;
                }
                ushort x = countdowntempx;
                ushort y = NTHO(message, 3);
                ushort z = countdowntempz;
                byte rotx = message[7];
                byte roty = message[8];
                pos = new ushort[3] { x, y, z };
                rot = new byte[2] { rotx, roty };
                if (countdowntempx != NTHO(message, 1) || countdowntempz != NTHO(message, 5))
                {
                    unchecked { this.SendPos((byte)-1, pos[0], pos[1], pos[2], rot[0], rot[1]); }
                }
            }
            else
            {
                ushort x = NTHO(message, 1);
                ushort y = NTHO(message, 3);
                ushort z = NTHO(message, 5);

                if (!this.referee && Server.noRespawn && Server.ZombieModeOn)
                {
                    if (this.pos[0] >= x + 70 || this.pos[0] <= x - 70)
                    {
                        unchecked { SendPos((byte)-1, pos[0], pos[1], pos[2], rot[0], rot[1]); }
                        return;
                    }
                    if (this.pos[2] >= z + 70 || this.pos[2] <= z - 70)
                    {
                        unchecked { SendPos((byte)-1, pos[0], pos[1], pos[2], rot[0], rot[1]); }
                        return;
                    }
                }
                try
                {
                    int xx, yy, zz; Random rand = new Random(); int size = 0;
                    Player.players.ForEach(delegate (Player player)
                    {
                        /*    #region Traps
                            for (xx = ((x / 32) - (size + 1 + player.trapUpgrade)); xx <= ((x / 32) + (size + 1 + player.trapUpgrade)); ++xx)
                                for (yy = ((y / 32) - (size + 1 + player.trapUpgrade)); yy <= ((y / 32) + (size + 1 + player.trapUpgrade)); ++yy)
                                    for (zz = ((z / 32) - (size + 1 + player.trapUpgrade)); zz <= ((z / 32) + (size + 1 + player.trapUpgrade)); ++zz)
                                    {
                                        if ((this.level.GetTile(Convert.ToUInt16((int)xx), Convert.ToUInt16((int)yy), Convert.ToUInt16((int)zz)) == 260))
                                        {
                                            if (player.trapsPlaced >= 1)
                                            {
                                                if (xx == (player.trapPlacement[0] / 32) && zz == (player.trapPlacement[2] / 32) && yy == (player.trapPlacement[1] / 32) && Server.pctf.getTeam(player) != Server.pctf.getTeam(this))
                                                {
                                                    player.SendMessage(c.gray + " - " + Server.DefaultColor + "Your trap has been activated!" + c.gray + " - ");
                                                    Level level = player.level;
                                                    if (!hasBeenTrapped)
                                                    {
                                                        hasBeenTrapped = true;
                                                        Player.SendMessage(this, c.gray + " - " + Server.DefaultColor + "You have been trapped! To get out break the mushroom" + c.gray + " - ");
                                                    }
                                                    return;
                                                }
                                            }
                                        }
                                    }
                            #endregion

                            #region Mines
                            for (xx = ((x / 32) - (size + 1 + (player.mineUpgrade + 1))); xx <= ((x / 32) + (size + 1 + (player.mineUpgrade + 1))); ++xx)
                                for (yy = ((y / 32) - (size + 2)); yy <= ((y / 32) + (size + 2)); ++yy)
                                    for (zz = ((z / 32) - (size + 1 + (player.mineUpgrade + 1))); zz <= ((z / 32) + (size + 1 + (player.mineUpgrade + 1))); ++zz)
                                    {
                                        if ((this.level.GetTile(Convert.ToUInt16((int)xx), Convert.ToUInt16((int)yy), Convert.ToUInt16((int)zz)) == 259))
                                        {
                                            if (player.minesPlaced >= 1)
                                            {
                                                if (xx == (player.minePlacement[0] / 32) && zz == (player.minePlacement[2] / 32) && yy == (player.minePlacement[1] / 32) && Server.pctf.getTeam(player) != Server.pctf.getTeam(this))
                                                {
                                                    Level level = player.level;
                                                    player.SendMessage(c.gray + " - " + Server.DefaultColor + "Your mine has been activated!" + c.gray + " - ");
                                                    player.minePlacement[0] = 0; player.minePlacement[1] = 0; player.minePlacement[2] = 0;
                                                    player.minesPlaced = player.minesPlaced - 1;
                                                    level.MakeExplosion(player.name, Convert.ToUInt16((int)(x / 32)), Convert.ToUInt16((int)(y / 32) - 1), Convert.ToUInt16((int)(z / 32)), 0, false, false, false);
                                                    level.placeBlock(Convert.ToUInt16((int)xx), Convert.ToUInt16((int)yy), Convert.ToUInt16((int)zz), Block.air);
                                                }
                                            }
                                        }
                                    }
                            #endregion*/
                    });
                }
                catch { }
                if (OnMove != null)
                    OnMove(this, x, y, z);
                if (PlayerMove != null)
                    PlayerMove(this, x, y, z);
                PlayerMoveEvent.Call(this, x, y, z);

                if (OnRotate != null)
                    OnRotate(this, rot);
                if (PlayerRotate != null)
                    PlayerRotate(this, rot);
                PlayerRotateEvent.Call(this, rot);
                if (cancelmove)
                {
                    unchecked { SendPos((byte)-1, pos[0], pos[1], pos[2], rot[0], rot[1]); }
                    return;
                }
                byte rotx = message[7];
                byte roty = message[8];
                pos = new ushort[3] { x, y, z };
                rot = new byte[2] { rotx, roty };
                /*if (!CheckIfInsideBlock())
{
clippos = pos;
cliprot = rot;
}*/
            }
        }

        public void RealDeath(ushort x, ushort y, ushort z)
        {
            ushort b = level.GetTile(x, (ushort)(y - 2), z);
            ushort b1 = level.GetTile(x, y, z);
            if (oldBlock != (ushort)(x + y + z))
            {
                if (Block.Convert(b) == Block.air)
                {
                    deathCount++;
                    deathblock = Block.air;
                    return;
                }
                else
                {
                    if (deathCount > level.fall && deathblock == Block.air)
                    {
                        HandleDeath(deathblock);
                        deathCount = 0;
                    }
                    else if (deathblock != Block.water)
                    {
                        deathCount = 0;
                    }
                }
            }

            switch (Block.Convert(b1))
            {
                case Block.water:
                case Block.waterstill:
                case Block.lava:
                case Block.lavastill:
                    deathCount++;
                    deathblock = Block.water;
                    if (deathCount > level.drown * 200)
                    {
                        HandleDeath(deathblock);
                        deathCount = 0;
                    }
                    break;
                default:
                    deathCount = 0;
                    break;
            }
        }

        public void CheckBlock(ushort x, ushort y, ushort z)
        {
            y = (ushort)Math.Round((decimal)(((y * 32) + 4) / 32));

            ushort b = this.level.GetTile(x, y, z);
            ushort b1 = this.level.GetTile(x, (ushort)((int)y - 1), z);

            if (Block.Mover(b) || Block.Mover(b1))
            {
                if (Block.DoorAirs(b) != 0)
                    level.Blockchange(x, y, z, Block.DoorAirs(b));
                if (Block.DoorAirs(b1) != 0)
                    level.Blockchange(x, (ushort)(y - 1), z, Block.DoorAirs(b1));

                if ((x + y + z) != oldBlock)
                {
                    if (b == Block.air_portal || b == Block.water_portal || b == Block.lava_portal)
                    {
                        HandlePortal(this, x, y, z, b);
                    }
                    else if (b1 == Block.air_portal || b1 == Block.water_portal || b1 == Block.lava_portal)
                    {
                        HandlePortal(this, x, (ushort)((int)y - 1), z, b1);
                    }

                    if (b == Block.MsgAir || b == Block.MsgWater || b == Block.MsgLava)
                    {
                        HandleMsgBlock(this, x, y, z, b);
                    }
                    else if (b1 == Block.MsgAir || b1 == Block.MsgWater || b1 == Block.MsgLava)
                    {
                        HandleMsgBlock(this, x, (ushort)((int)y - 1), z, b1);
                    }
                }
            }
            if ((b == Block.tntexplosion || b1 == Block.tntexplosion) && Server.CTFOnlyServer)
            {
                return;
            }

            if ((b == Block.tntexplosion || b1 == Block.tntexplosion) && PlayingTntWars) { }
            else if (Block.Death(b)) HandleDeath(b); else if (Block.Death(b1)) HandleDeath(b1);
        }

        public LavaSurvival lavasurvival;

        public void HandleDeath(ushort b, string customMessage = "", bool explode = false)
        {
            ushort x = (ushort)(pos[0] / (ushort)32);
            ushort y = (ushort)(pos[1] / 32);
            ushort z = (ushort)(pos[2] / 32);
            ushort y1 = (ushort)((int)pos[1] / 32 - 1);
            ushort xx = pos[0];
            ushort yy = pos[1];
            ushort zz = pos[2];
            if (OnDeath != null)
                OnDeath(this, b);
            if (PlayerDeath != null)
                PlayerDeath(this, b);
            OnPlayerDeathEvent.Call(this, b);
            if (Server.lava.active && Server.lava.HasPlayer(this) && Server.lava.IsPlayerDead(this))
                return;
            if (lastDeath.AddSeconds(2) < DateTime.Now)
            {

                if (level.Killer && !invincible)
                {

                    switch (b)
                    {
                        case Block.tntexplosion: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " &cblew into pieces.", false); break;
                        case Block.deathair: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " walked into &cnerve gas and suffocated.", false); break;
                        case Block.deathwater:
                        case Block.activedeathwater: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " stepped in &dcold water and froze.", false); break;
                        case Block.deathlava:
                        case Block.activedeathlava:
                        case Block.fastdeathlava: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " stood in &cmagma and melted.", false); break;
                        case Block.magma: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " was hit by &cflowing magma and melted.", false); break;
                        case Block.geyser: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " was hit by &cboiling water and melted.", false); break;
                        case Block.birdkill: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " was hit by a &cphoenix and burnt.", false); break;
                        case Block.train: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " was hit by a &ctrain.", false); break;
                        case Block.fishshark: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " was eaten by a &cshark.", false); break;
                        case Block.fire: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " burnt to a &ccrisp.", false); break;
                        case Block.rockethead: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " was &cin a fiery explosion.", false); level.MakeExplosion(x, y, z, 0); break;
                        case Block.zombiebody: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " died due to lack of &5brain.", false); break;
                        case Block.creeper: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " was killed &cb-SSSSSSSSSSSSSS", false); level.MakeExplosion(x, y, z, 1); break;
                        case Block.air: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " hit the floor &chard.", false); break;
                        case Block.water: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " &cdrowned.", false); break;
                        case Block.Zero: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " was &cterminated", false); break;
                        case Block.fishlavashark: GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + " was eaten by a ... LAVA SHARK?!", false); break;
                        case Block.rock:
                            if (explode) level.MakeExplosion(x, y, z, 1);
                            GlobalChat(this, this.color + this.prefix + this.name + Server.DefaultColor + customMessage, false);
                            break;
                        case Block.stone:
                            if (explode) level.MakeExplosion(x, y, z, 1);
                            GlobalChatLevel(this, this.color + this.prefix + this.name + Server.DefaultColor + customMessage, false);
                            break;
                    }
                    if (ironmanActivated)
                    {
                        Player.GlobalMessage(this.group.color + name + " " + c.lime + "failed the IRONMAN challenge!");
                        ironmanFailed = true;
                        ironmanActivated = false;
                        if (lavasurvival.lifeNum <= 2)
                            lavasurvival.lifeNum = 0;
                        else
                            lavasurvival.lifeNum = lavasurvival.lifeNum / 2;

                        if (money <= 2)
                            money = 0;
                        else
                            money = (int)Math.Round(money / 1.2);
                    }
                    /*  if(pteam != 0)
                      {
                          Server.pctf.sendToTeamSpawn(this);
                      }*/
                    if (CountdownGame.playersleftlist.Contains(this))
                    {
                        CountdownGame.Death(this);
                        Command.all.Find("spawn").Use(this, "");
                    }
                    else if (PlayingTntWars)
                    {
                        TntWarsKillStreak = 0;
                        TntWarsScoreMultiplier = 1f;
                    }
                    else if (Server.lava.active && Server.lava.HasPlayer(this))
                    {
                        if (!Server.lava.IsPlayerDead(this))
                        {
                            Server.lava.KillPlayer(this);
                            Command.all.Find("spawn").Use(this, "");
                        }
                    }
                    else
                    {
                        if (pteam == 0)
                        {
                            Command.all.Find("spawn").Use(this, "");
                            overallDeath++;
                        }
                    }

                    if (Server.deathcount)
                        if (overallDeath > 0 && overallDeath % 10 == 0) GlobalChat(this, this.color + this.prefix + this.name + Server.DefaultColor + " has died &3" + overallDeath + " times", false);
                }
                lastDeath = DateTime.Now;

            }
        }

        /* void HandleFly(Player p, ushort x, ushort y, ushort z) {
FlyPos pos;

ushort xx; ushort yy; ushort zz;

TempFly.Clear();

if (!flyGlass) y = (ushort)(y + 1);

for (yy = y; yy >= (ushort)(y - 1); --yy)
for (xx = (ushort)(x - 2); xx <= (ushort)(x + 2); ++xx)
for (zz = (ushort)(z - 2); zz <= (ushort)(z + 2); ++zz)
if (p.level.GetTile(xx, yy, zz) == Block.air) {
pos.x = xx; pos.y = yy; pos.z = zz;
TempFly.Add(pos);
}

FlyBuffer.ForEach(delegate(FlyPos pos2) {
try { if (!TempFly.Contains(pos2)) SendBlockchange(pos2.x, pos2.y, pos2.z, Block.air); } catch { }
});

FlyBuffer.Clear();

TempFly.ForEach(delegate(FlyPos pos3){
FlyBuffer.Add(pos3);
});

if (flyGlass) {
FlyBuffer.ForEach(delegate(FlyPos pos1) {
try { SendBlockchange(pos1.x, pos1.y, pos1.z, Block.glass); } catch { }
});
} else {
FlyBuffer.ForEach(delegate(FlyPos pos1) {
try { SendBlockchange(pos1.x, pos1.y, pos1.z, Block.waterstill); } catch { }
});
}
} */

        void SendWomUsers()
        {
            Player.players.ForEach(delegate (Player p)
            {
                if (p != this)
                {
                    byte[] buffer = new byte[65];
                    Player.StringFormat("^detail.user.here=" + p.color + p.name, 64).CopyTo(buffer, 1);
                    SendRaw(OpCode.Message, buffer);
                    buffer = null;
                }
            });
        }
        void HandleChat(byte[] message)
        {
            try
            {
                if (!loggedIn) return;

                //byte[] message = (byte[])m;
                string text = enc.GetString(message, 1, 64).Trim();
                // removing nulls (matters for the /womid messages)
                text = text.Trim('\0');


                if (MessageHasBadColorCodes(this, text)) return;
                if (storedMessage != "")
                {
                    if (!text.EndsWith(">") && !text.EndsWith("<"))
                    {
                        text = storedMessage.Replace("|>|", " ").Replace("|<|", "") + text;
                        storedMessage = "";
                    }
                }
                if (text.StartsWith(">") || text.StartsWith("<")) return;
                if (text.EndsWith(">"))
                {
                    storedMessage += text.Replace(">", "|>|");
                    SendMessage(c.teal + "Partial message: " + c.white + storedMessage.Replace("|>|", " ").Replace("|<|", ""));
                    return;
                }
                if (text.EndsWith("<"))
                {
                    storedMessage += text.Replace("<", "|<|");
                    SendMessage(c.teal + "Partial message: " + c.white + storedMessage.Replace("|<|", "").Replace("|>|", " "));
                    return;
                }
                if (Regex.IsMatch(text, "%[^a-f0-9]"))//This causes all players to crash!
                {
                    SendMessage(this, "You're not allowed to send that message!");
                    return;
                }

                text = Regex.Replace(text, @"\s\s+", " ");
                if (text.Any(ch => ch < 32 || ch >= 127 || ch == '&'))
                {
                    Kick("Illegal character in chat message!");
                    return;
                }
                if (text.Length == 0)
                    return;
                afkCount = 0;

                if (text != "/afk")
                {
                    if (Server.afkset.Contains(this.name))
                    {
                        Server.afkset.Remove(this.name);
                        Player.GlobalMessage("-" + this.color + this.name + Server.DefaultColor + "- is no longer AFK");
                        //IRCBot.Say(this.name + " is no longer AFK");
                    }
                }
                // This will allow people to type
                // //Command
                // and in chat it will appear as
                // /Command
                // Suggested by McMrCat
                if (text.StartsWith("//"))
                {
                    text = text.Remove(0, 1);
                    goto hello;
                }
                //This will make / = /repeat
                //For lazy people :P
                if (text == "/")
                {
                    HandleCommand("repeat", "");
                    return;
                }
                if (text[0] == '/' || text[0] == '!')
                {
                    text = text.Remove(0, 1);

                    int pos = text.IndexOf(' ');
                    if (pos == -1)
                    {
                        HandleCommand(text.ToLower(), "");
                        return;
                    }
                    string cmd = text.Substring(0, pos).ToLower();
                    string msg = text.Substring(pos + 1);
                    HandleCommand(cmd, msg);
                    return;
                }
            hello:
                // People who are muted can't speak or vote
                if (muted) { this.SendMessage("You are muted."); return; } //Muted: Only allow commands

                // Lava Survival map vote recorder
                if (Server.lava.HasPlayer(this) && Server.lava.HasVote(text.ToLower()))
                {
                    if (Server.lava.AddVote(this, text.ToLower()))
                    {
                        SendMessage("Your vote for &5" + text.ToLower().UppercaseFirst() + Server.DefaultColor + " has been placed. Thanks!");
                        Server.lava.map.ChatLevelOps(name + " voted for &5" + text.ToLower().UppercaseFirst() + Server.DefaultColor + ".");
                        return;
                    }
                    else
                    {
                        SendMessage("&cYou already voted!");
                        return;
                    }
                }

                //CmdVoteKick core vote recorder
                if (Server.voteKickInProgress && text.Length == 1)
                {
                    if (text.ToLower() == "y")
                    {
                        this.voteKickChoice = VoteKickChoice.Yes;
                        SendMessage("Thanks for voting!");
                        return;
                    }
                    if (text.ToLower() == "n")
                    {
                        this.voteKickChoice = VoteKickChoice.No;
                        SendMessage("Thanks for voting!");
                        return;
                    }
                }

                // Put this after vote collection so that people can vote even when chat is moderated
                if (Server.chatmod && !this.voice) { this.SendMessage("Chat moderation is on, you cannot speak."); return; }

                // Filter out bad words
                if (Server.profanityFilter == true)
                {
                    text = ProfanityFilter.Parse(text);
                }

                if (Server.checkspam == true)
                {
                    //if (consecutivemessages == 0)
                    //{
                    // consecutivemessages++;
                    //}
                    if (Player.lastMSG == this.name)
                    {
                        consecutivemessages++;
                    }
                    else
                    {
                        consecutivemessages--;
                    }

                    if (this.consecutivemessages >= Server.spamcounter)
                    {
                        int total = Server.mutespamtime;
                        Command.all.Find("mute").Use(null, this.name);
                        Player.GlobalMessage(this.name + " has been &0muted &efor spamming!");
                        muteTimer.Elapsed += delegate
                        {
                            total--;
                            if (total <= 0)
                            {
                                muteTimer.Stop();
                                if (this.muted == true)
                                {
                                    Command.all.Find("mute").Use(null, this.name);
                                }
                                this.consecutivemessages = 0;
                                Player.SendMessage(this, "Remember, no &cspamming &e" + "next time!");
                            }
                        };
                        muteTimer.Start();
                        return;
                    }
                }
                Player.lastMSG = this.name;

                if (text.Length >= 2 && text[0] == '@' && text[1] == '@')
                {
                    text = text.Remove(0, 2);
                    if (text.Length < 1) { SendMessage("No message entered"); return; }
                    SendChat(this, Server.DefaultColor + "[<] Console: &f" + text);
                    Server.s.Log("[>] " + this.name + ": " + text);
                    return;
                }
                if (text[0] == '@' || whisper)
                {
                    string newtext = text;
                    if (text[0] == '@') newtext = text.Remove(0, 1).Trim();

                    if (whisperTo == "")
                    {
                        int pos = newtext.IndexOf(' ');
                        if (pos != -1)
                        {
                            string to = newtext.Substring(0, pos);
                            string msg = newtext.Substring(pos + 1);
                            HandleQuery(to, msg); return;
                        }
                        else
                        {
                            SendMessage("No message entered");
                            return;
                        }
                    }
                    else
                    {
                        HandleQuery(whisperTo, newtext);
                        return;
                    }
                }
                if (text[0] == '#' || opchat)
                {
                    string newtext = text;
                    if (text[0] == '#') newtext = text.Remove(0, 1).Trim();

                    GlobalMessageOps("To Ops &f-" + color + name + "&f- " + newtext);
                    if (group.Permission < Server.opchatperm && !isStaff)
                        SendMessage("To Ops &f-" + color + name + "&f- " + newtext);
                    Server.s.Log("(OPs): " + name + ": " + newtext);
                    //Server.s.OpLog("(OPs): " + name + ": " + newtext);
                    //IRCBot.Say(name + ": " + newtext, true);
                    //Server.IRC.Say(name + ": " + newtext, true);
                    return;
                }
                if (text[0] == '+' || adminchat)
                {
                    string newtext = text;
                    if (text[0] == '+') newtext = text.Remove(0, 1).Trim();

                    GlobalMessageAdmins("To Admins &f-" + color + name + "&f- " + newtext); //to make it easy on remote
                    if (group.Permission < Server.adminchatperm && !isStaff)
                        SendMessage("To Admins &f-" + color + name + "&f- " + newtext);
                    Server.s.Log("(Admins): " + name + ": " + newtext);
                    // Server.IRC.Say(name + ": " + newtext, true);
                    return;
                }

                /*   if ((text[0] == '^' || teamchat) && Server.CTFModeOn)
                   {
                       string newtext = text;
                       if (text[0] == '^') newtext = text.Remove(0, 1).Trim();

                    //   if (Server.pctf.getTeam(this) != null)
                      //     GlobalMessageTeam(color + name + "&f- " + newtext, Server.pctf.getTeam(this));  //to make it easy on remote
                       //else
                           Player.SendMessage(this, "You must be on a team to use team chat!");
                       Server.s.Log("(Team): " + name + ": " + newtext);
                       //IRCBot.Say(name + ": " + newtext, true);
                       Server.IRC.Say(name + ": " + newtext, false);
                       return;
                   }*/

                if (InGlobalChat)
                {
                    Command.all.Find("global").Use(this, text); //Didn't want to rewrite the whole command... you lazy bastard :3
                    return;
                }

                if (text[0] == ':')
                {
                    if (PlayingTntWars)
                    {
                        string newtext = text;
                        if (text[0] == ':') newtext = text.Remove(0, 1).Trim();
                        TntWarsGame it = TntWarsGame.GetTntWarsGame(this);
                        if (it.GameMode == TntWarsGame.TntWarsGameMode.TDM)
                        {
                            TntWarsGame.player pl = it.FindPlayer(this);
                            foreach (TntWarsGame.player p in it.Players)
                            {
                                if (pl.Red && p.Red) SendMessage(p.p, "To Team " + c.red + "-" + color + name + c.red + "- " + Server.DefaultColor + newtext);
                                if (pl.Blue && p.Blue) SendMessage(p.p, "To Team " + c.blue + "-" + color + name + c.blue + "- " + Server.DefaultColor + newtext);
                            }
                            Server.s.Log("[TNT Wars] [TeamChat (" + (pl.Red ? "Red" : "Blue") + ") " + name + " " + newtext);
                            return;
                        }
                    }
                }

                /*if (this.teamchat)
{
if (team == Block.air)
{
Player.SendMessage(this, "You are not on a team.");
return;
}
foreach (Player p in team.players)
{
Player.SendMessage(p, "(" + team.teamstring + ") " + this.color + this.name + ":&f " + text);
}
return;
}*/
                if (this.joker)
                {
                    if (File.Exists("text/joker.txt"))
                    {
                        Server.s.Log("<JOKER>: " + this.name + ": " + text);
                        Player.GlobalMessageOps(Server.DefaultColor + "<&aJ&bO&cK&5E&9R" + Server.DefaultColor + ">: " + this.color + this.name + ":&f " + text);
                        FileInfo jokertxt = new FileInfo("text/joker.txt");
                        StreamReader stRead = jokertxt.OpenText();
                        List<string> lines = new List<string>();
                        Random rnd = new Random();
                        int i = 0;

                        while (!(stRead.Peek() == -1))
                            lines.Add(stRead.ReadLine());

                        stRead.Close();
                        stRead.Dispose();

                        if (lines.Count > 0)
                        {
                            i = rnd.Next(lines.Count);
                            text = lines[i];
                        }

                    }
                    else { File.Create("text/joker.txt").Dispose(); }

                }

                //chatroom stuff
                if (this.Chatroom != null)
                {
                    ChatRoom(this, text, true, this.Chatroom);
                    return;
                }

                if (!level.worldChat)
                {
                    Server.s.Log("<" + name + ">[level] " + text);
                    GlobalChatLevel(this, text, true);
                    return;
                }

                if (text[0] == '%')
                {
                    string newtext = text;
                    if (!Server.worldChat)
                    {
                        newtext = text.Remove(0, 1).Trim();
                        GlobalChatWorld(this, newtext, true);
                    }
                    else
                    {
                        GlobalChat(this, newtext);
                    }
                    Server.s.Log("<" + name + "> " + newtext);
                    //IRCBot.Say("<" + name + "> " + newtext);
                    if (OnChat != null)
                        OnChat(this, text);
                    if (PlayerChat != null)
                        PlayerChat(this, text);
                    OnPlayerChatEvent.Call(this, text);
                    return;
                }
                Server.s.Log("<" + name + "> " + text);
                if (OnChat != null)
                    OnChat(this, text);
                if (PlayerChat != null)
                    PlayerChat(this, text);
                OnPlayerChatEvent.Call(this, text);
                if (cancelchat)
                {
                    cancelchat = false;
                    return;
                }
                if (Server.worldChat)
                {
                    GlobalChat(this, text);
                }
                else
                {
                    GlobalChatLevel(this, text, true);
                }

                //IRCBot.Say(name + ": " + text);
            }
            catch (Exception e) { Server.ErrorLog(e); Player.GlobalMessage("An error occurred: " + e.Message); }
        }
        public void HandleCommand(string cmd, string message)
        {
            try
            {
                if (Server.verifyadmins)
                {
                    if (cmd.ToLower() == "setpass")
                    {
                        Command.all.Find(cmd).Use(this, message);
                        return;
                    }
                    if (cmd.ToLower() == "pass")
                    {
                        Command.all.Find(cmd).Use(this, message);
                        return;
                    }
                }
                if (Server.agreetorulesonentry)
                {
                    if (cmd.ToLower() == "agree")
                    {
                        Command.all.Find(cmd).Use(this, String.Empty);
                        Server.s.CommandUsed(this.name + " used /agree");
                        return;
                    }
                    if (cmd.ToLower() == "rules")
                    {
                        Command.all.Find(cmd).Use(this, String.Empty);
                        Server.s.CommandUsed(this.name + " used /rules");
                        return;
                    }
                    if (cmd.ToLower() == "disagree")
                    {
                        Command.all.Find(cmd).Use(this, String.Empty);
                        Server.s.CommandUsed(this.name + " used /disagree");
                        return;
                    }
                }

                if (cmd == String.Empty) { SendMessage("No command entered."); return; }

                if (Server.agreetorulesonentry && !agreed)
                {
                    SendMessage("You must read /rules then agree to them with /agree!");
                    return;
                }
                if (jailed)
                {
                    SendMessage("You cannot use any commands while jailed.");
                    return;
                }
                if (Server.verifyadmins)
                {
                    if (this.adminpen)
                    {
                        this.SendMessage("&cYou must use &a/pass [Password]&c to verify!");
                        return;
                    }
                }

                if (cmd.ToLower() == "care") { SendMessage("Dmitchell94 now loves you with all his heart."); return; }
                if (cmd.ToLower() == "facepalm") { SendMessage("Fenderrock87's bot army just simultaneously facepalm'd at your use of this command."); return; }
                if (cmd.ToLower() == "alpaca") { SendMessage("Leitrean's Alpaca Army just raped your woman and pillaged your villages!"); return; }
                //DO NOT REMOVE THE TWO COMMANDS BELOW, /PONY AND /RAINBOWDASHLIKESCOOLTHINGS. -EricKilla
                if (cmd.ToLower() == "pony")
                {
                    if (ponycount < 2)
                    {
                        GlobalMessage(this.color + this.name + Server.DefaultColor + " just so happens to be a proud brony! Everyone give " + this.color + this.name + Server.DefaultColor + " a brohoof!");
                        ponycount += 1;
                    }
                    else
                    {
                        SendMessage("You have used this command 2 times. You cannot use it anymore! Sorry, Brony!");
                    }
                    if (OnBecomeBrony != null)
                        OnBecomeBrony(this);
                    return;
                }
                if (cmd.ToLower() == "rainbowdashlikescoolthings")
                {
                    if (rdcount < 2)
                    {
                        GlobalMessage("&1T&2H&3I&4S &5S&6E&7R&8V&9E&aR &bJ&cU&dS&eT &fG&0O&1T &22&30 &4P&CE&7R&DC&EE&9N&1T &5C&6O&7O&8L&9E&aR&b!");
                        rdcount += 1;
                    }
                    else
                    {
                        SendMessage("You have used this command 2 times. You cannot use it anymore! Sorry, Brony!");
                    }
                    if (OnSonicRainboom != null)
                        OnSonicRainboom(this);
                    return;
                }

                if (CommandHasBadColourCodes(this, message))
                    return;
                string foundShortcut = Command.all.FindShort(cmd);
                if (foundShortcut != "") cmd = foundShortcut;
                if (OnCommand != null)
                    OnCommand(cmd, this, message);
                if (PlayerCommand != null)
                    PlayerCommand(cmd, this, message);
                OnPlayerCommandEvent.Call(cmd, this, message);
                if (cancelcommand)
                {
                    cancelcommand = false;
                    return;
                }
                try
                {
                    int foundCb = int.Parse(cmd);
                    if (messageBind[foundCb] == null) { SendMessage("No CMD is stored on /" + cmd); return; }
                    message = messageBind[foundCb] + " " + message;
                    message = message.TrimEnd(' ');
                    cmd = cmdBind[foundCb];
                }
                catch { }

                Command command = Command.all.Find(cmd);
                //Group old = null;
                if (command != null)
                {
                    //this part checks if MCForge staff are able to USE protection commands
                    /*if (isProtected && Server.ProtectOver.Contains(cmd.ToLower())) {
                        old = Group.findPerm(this.group.Permission);
                        this.group = Group.findPerm(LevelPermission.Nobody);
                    }*/

                    if (Player.CommandProtected(cmd.ToLower(), message))
                    {
                        SendMessage("Cannot use command, player has protection level: " + Server.forgeProtection);
                        Server.s.CommandUsed(name + " used /" + cmd + " " + message);
                        return;
                    }

                    if (group.CanExecute(command))
                    {
                        if (cmd != "repeat") lastCMD = cmd + " " + message;
                        if (level.name.Contains("Museum " + Server.DefaultColor))
                        {
                            if (!command.museumUsable)
                            {
                                SendMessage("Cannot use this command while in a museum!");
                                return;
                            }
                        }
                        if (this.joker == true || this.muted == true)
                        {
                            if (cmd.ToLower() == "me")
                            {
                                SendMessage("Cannot use /me while muted or jokered.");
                                return;
                            }
                        }
                        if (cmd.ToLower() != "setpass" || cmd.ToLower() != "pass")
                        {
                            Server.s.CommandUsed(name + " used /" + cmd + " " + message);
                        }

                        try
                        { //opstats patch (since 5.5.11)
                          //         if (Server.opstats.Contains(cmd.ToLower()) || (cmd.ToLower() == "review" && message.ToLower() == "next" && Server.reviewlist.Count > 0))
                          //        {
                          //          Database.AddParams("@Time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                          //              Database.AddParams("@Name", name);
                          //              Database.AddParams("@Cmd", cmd);
                          //               Database.AddParams("@Cmdmsg", message);
                          //              Database.executeQuery("INSERT INTO Opstats (Time, Name, Cmd, Cmdmsg) VALUES (@Time, @Name, @Cmd, @Cmdmsg)");
                          // }
                        }
                        catch { }

                        this.commThread = new Thread(new ThreadStart(delegate
                        {
                            try
                            {
                                command.Use(this, message);
                            }
                            catch (Exception e)
                            {
                                Server.ErrorLog(e);
                                Player.SendMessage(this, "An error occured when using the command!");
                                Player.SendMessage(this, e.GetType().ToString() + ": " + e.Message);
                            }
                            //finally { if (old != null) this.group = old; }
                        }));
                        commThread.Start();
                    }
                    else { SendMessage("You are not allowed to use \"" + cmd + "\"!"); }
                }
                else if (Block.Ushort(cmd.ToLower()) != Block.Zero)
                {
                    HandleCommand("mode", cmd.ToLower());
                }
                else
                {
                    bool retry = true;

                    switch (cmd.ToLower())
                    { //Check for command switching
                        case "guest": message = message + " " + cmd.ToLower(); cmd = "setrank"; break;
                        case "builder": message = message + " " + cmd.ToLower(); cmd = "setrank"; break;
                        case "advbuilder":
                        case "adv": message = message + " " + cmd.ToLower(); cmd = "setrank"; break;
                        case "operator":
                        case "op": message = message + " " + cmd.ToLower(); cmd = "setrank"; break;
                        case "super":
                        case "superop": message = message + " " + cmd.ToLower(); cmd = "setrank"; break;
                        case "cut": cmd = "copy"; message = "cut"; break;
                        case "admins": message = "superop"; cmd = "viewranks"; break;
                        case "ops": message = "op"; cmd = "viewranks"; break;
                        case "banned": message = cmd; cmd = "viewranks"; break;

                        case "ps": message = "ps " + message; cmd = "map"; break;

                        //How about we start adding commands from other softwares
                        //and seamlessly switch here?
                        case "bhb":
                        case "hbox": cmd = "cuboid"; message = "hollow"; break;
                        case "blb":
                        case "box": cmd = "cuboid"; break;
                        case "sphere": cmd = "spheroid"; break;
                        case "cmdlist":
                        case "commands": cmd = "help"; message = "old"; break;
                        case "cmdhelp": cmd = "help"; break;
                        case "worlds":
                        case "mapsave": cmd = "save"; break;
                        case "mapload": cmd = "load"; break;
                        case "colour": cmd = "color"; break;
                        case "materials": cmd = "blocks"; break;

                        default: retry = false; break; //Unknown command, then
                    }

                    if (retry) HandleCommand(cmd, message);
                    else SendMessage("Unknown command \"" + cmd + "\"!");
                }
            }
            catch (Exception e) { Server.ErrorLog(e); SendMessage("Command failed."); }
        }
        void HandleQuery(string to, string message)
        {
            Player p = Find(to);
            if (p == this) { SendMessage("Trying to talk to yourself, huh?"); return; }
            if (p == null) { SendMessage("Could not find player."); return; }
            if (p.hidden) { if (this.hidden == false) { Player.SendMessage(p, "Could not find player."); } }
            if (p.ignoreglobal == true)
            {
                if (Server.globalignoreops == false)
                {
                    if (this.group.Permission >= Server.opchatperm)
                    {
                        if (p.group.Permission < this.group.Permission)
                        {
                            Server.s.Log(name + " @" + p.name + ": " + message);
                            SendChat(this, Server.DefaultColor + "[<] " + p.color + p.prefix + p.name + ": &f" + message);
                            SendChat(p, "&9[>] " + this.color + this.prefix + this.name + ": &f" + message);
                            return;
                        }
                    }
                }
                Server.s.Log(name + " @" + p.name + ": " + message);
                SendChat(this, Server.DefaultColor + "[<] " + p.color + p.prefix + p.name + ": &f" + message);
                return;
            }
            foreach (string ignored2 in p.listignored)
            {
                if (ignored2 == this.name)
                {
                    Server.s.Log(name + " @" + p.name + ": " + message);
                    SendChat(this, Server.DefaultColor + "[<] " + p.color + p.prefix + p.name + ": &f" + message);
                    return;
                }
            }
            if (p != null && !p.hidden || p.hidden && this.group.Permission >= p.group.Permission)
            {
                Server.s.Log(name + " @" + p.name + ": " + message);
                SendChat(this, Server.DefaultColor + "[<] " + p.color + p.prefix + p.name + ": &f" + message);
                SendChat(p, "&9[>] " + this.color + this.prefix + this.name + ": &f" + message);
            }
            else { SendMessage("Player \"" + to + "\" doesn't exist!"); }
        }
        #endregion
        #region == OUTGOING ==
        public void SendRaw(OpCode id)
        {
            SendRaw(id, new byte[0]);
        }
        public void SendRaw(OpCode id, byte send)
        {
            SendRaw(id, new byte[] { send });
        }
        public void SendRaw(OpCode id, byte[] send)
        {
            // Abort if socket has been closed
            if (socket == null || !socket.Connected)
                return;
            byte[] buffer = new byte[send.Length + 1];
            buffer[0] = (byte)id;
            for (int i = 0; i < send.Length; i++)
            {
                buffer[i + 1] = send[i];
            }
            try
            {
                socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, delegate (IAsyncResult result) { }, Block.air);
                buffer = null;
            }
            catch (SocketException)
            {
                buffer = null;
                Disconnect();
            }
        }


        public static void SendMessage(Player p, string message)
        {
            if (p == null) { Server.s.Log(message); return; }
            SendMessage(p, MessageType.Chat, message, true);
        }
        public static void SendMessage(Player p, MessageType type, string message, bool colorParse)
        {
            if (p == null)
            {
                if (storeHelp)
                {
                    storedHelp += message + "\r\n";
                }
                else
                {
                    if (!Server.irc || String.IsNullOrEmpty(Server.IRC.usedCmd))
                        Server.s.Log(message);
                    else
                        Server.IRC.Pm(Server.IRC.usedCmd, message);
                    //IRCBot.Say(message, true);
                }
                return;
            }

            p.SendMessage(type, Server.DefaultColor + message, colorParse);
        }

        public void SendMessage(string message)
        {
            SendMessage(message, true);
        }
        public void SendMessage(string message, bool colorParse)
        {
            if (this == null) { Server.s.Log(message); return; }
            unchecked { SendMessage(MessageType.Chat, Server.DefaultColor + message, colorParse); }
        }
        public void SendChat(Player p, string message)
        {
            if (this == null) { Server.s.Log(message); return; }
            Player.SendMessage(p, message);
        }
        public void SendMessage(byte id, string message)
        {
            SendMessage(MessageType.Chat, message, true);
        }

        public enum MessageType
        {
            Chat = (byte)0,
            Status1 = (byte)1,
            Status2 = (byte)2,
            Status3 = (byte)3,
            BottomRight1 = (byte)11,
            BottomRight2 = (byte)12,
            BottomRight3 = (byte)13,
            Announcement = (byte)100
        }

        public void SendMessage(MessageType type, string message, bool colorParse)
        {
            if (this == null)
            {
                Server.s.Log(message);
                return;
            }
            if (ZoneSpam.AddSeconds(2) > DateTime.Now && message.Contains("This zone belongs to "))
                return;

            byte[] buffer = new byte[65];
            unchecked
            {
                buffer[0] = (byte)type;
            }

            StringBuilder sb = new StringBuilder(message);

            if (colorParse)
            {
                for (int i = 0; i < 10; i++)
                {
                    sb.Replace("%" + i, "&" + i);
                    sb.Replace("&" + i + " &", " &");
                }
                for (char ch = 'a'; ch <= 'f'; ch++)
                {
                    sb.Replace("%" + ch, "&" + ch);
                    sb.Replace("&" + ch + " &", " &");
                }
                // Begin fix to replace all invalid color codes typed in console or chat with "."
                for (char ch = (char)0; ch <= (char)47; ch++) // Characters that cause clients to disconnect
                    sb.Replace("&" + ch, String.Empty);
                for (char ch = (char)58; ch <= (char)96; ch++) // Characters that cause clients to disconnect
                    sb.Replace("&" + ch, String.Empty);
                for (char ch = (char)103; ch <= (char)127; ch++) // Characters that cause clients to disconnect
                    sb.Replace("&" + ch, String.Empty);
                // End fix
            }

            if (Server.dollardollardollar)
                sb.Replace("$name", "$" + name);
            else
                sb.Replace("$name", name);
            sb.Replace("$date", DateTime.Now.ToString("yyyy-MM-dd"));
            sb.Replace("$time", DateTime.Now.ToString("HH:mm:ss"));
            sb.Replace("$ip", ip);
            sb.Replace("$serverip", IsLocalIpAddress(ip) ? ip : Server.IP);
            if (colorParse)
                sb.Replace("$color", color);
            sb.Replace("$rank", group.name);
            sb.Replace("$level", level.name);
            sb.Replace("$deaths", overallDeath.ToString());
            sb.Replace("$money", money.ToString());
            sb.Replace("$blocks", overallBlocks.ToString());
            sb.Replace("$first", firstLogin.ToString());
            sb.Replace("$kicked", totalKicked.ToString());
            sb.Replace("$server", Server.name);
            sb.Replace("$motd", Server.motd);
            sb.Replace("$banned", Player.GetBannedCount().ToString());
            sb.Replace("$irc", Server.ircServer + " > " + Server.ircChannel);

            foreach (var customReplacement in Server.customdollars)
            {
                if (!customReplacement.Key.StartsWith("//"))
                {
                    try
                    {
                        sb.Replace(customReplacement.Key, customReplacement.Value);
                    }
                    catch
                    {
                    }
                }
            }

            if (Server.parseSmiley && parseSmiley)
            {
                sb.Replace(":)", "(darksmile)");
                sb.Replace(":D", "(smile)");
                sb.Replace("<3", "(heart)");
            }

            byte[] stored = new byte[1];

            stored[0] = (byte)1;
            sb.Replace("(darksmile)", enc.GetString(stored));
            stored[0] = (byte)2;
            sb.Replace("(smile)", enc.GetString(stored));
            stored[0] = (byte)3;
            sb.Replace("(heart)", enc.GetString(stored));
            stored[0] = (byte)4;
            sb.Replace("(diamond)", enc.GetString(stored));
            stored[0] = (byte)7;
            sb.Replace("(bullet)", enc.GetString(stored));
            stored[0] = (byte)8;
            sb.Replace("(hole)", enc.GetString(stored));
            stored[0] = (byte)11;
            sb.Replace("(male)", enc.GetString(stored));
            stored[0] = (byte)12;
            sb.Replace("(female)", enc.GetString(stored));
            stored[0] = (byte)15;
            sb.Replace("(sun)", enc.GetString(stored));
            stored[0] = (byte)16;
            sb.Replace("(right)", enc.GetString(stored));
            stored[0] = (byte)17;
            sb.Replace("(left)", enc.GetString(stored));
            stored[0] = (byte)19;
            sb.Replace("(double)", enc.GetString(stored));
            stored[0] = (byte)22;
            sb.Replace("(half)", enc.GetString(stored));
            stored[0] = (byte)24;
            sb.Replace("(uparrow)", enc.GetString(stored));
            stored[0] = (byte)25;
            sb.Replace("(downarrow)", enc.GetString(stored));
            stored[0] = (byte)26;
            sb.Replace("(rightarrow)", enc.GetString(stored));
            stored[0] = (byte)30;
            sb.Replace("(up)", enc.GetString(stored));
            stored[0] = (byte)31;
            sb.Replace("(down)", enc.GetString(stored));

            message = ReplaceEmoteKeywords(sb.ToString());
            if (HasBadColorCodes(message))
                return;
            int totalTries = 0;
            if (MessageRecieve != null)
                MessageRecieve(this, message);
            if (OnMessageRecieve != null)
                OnMessageRecieve(this, message);
            OnMessageRecieveEvent.Call(this, message);
            if (cancelmessage)
            {
                cancelmessage = false;
                return;
            }
        retryTag: try
            {
                foreach (string line in Wordwrap(message))
                {
                    string newLine = line;
                    if (newLine.TrimEnd(' ')[newLine.TrimEnd(' ').Length - 1] < '!')
                    {
                        //For some reason, this did the opposite
                        if (!HasExtension("EmoteFix"))
                        {
                            newLine += '\'';
                        }
                        else
                        {
                        }

                    }

                    if (HasBadColorCodes(newLine))
                        continue;

                    StringFormat(newLine, 64).CopyTo(buffer, 1);
                    SendRaw(OpCode.Message, buffer);
                }
            }
            catch (Exception e)
            {
                message = "&f" + message;
                totalTries++;
                if (totalTries < 10) goto retryTag;
                else Server.ErrorLog(e);
            }
        }

        public void SendMotd()
        {
            byte[] buffer = new byte[130];
            buffer[0] = (byte)8;
            StringFormat(Server.name, 64).CopyTo(buffer, 1);

            if (Server.UseTextures)
                StringFormat("&0cfg=" + (IsLocalIpAddress(ip) ? ip : Server.IP) + ":" + Server.port + "/" + level.name + "~motd", 64).CopyTo(buffer, 65);
            else
            {
                if (!String.IsNullOrEmpty(group.MOTD)) StringFormat(group.MOTD, 64).CopyTo(buffer, 65);
                else StringFormat(Server.motd, 64).CopyTo(buffer, 65);
            }

            if (Block.canPlace(this, Block.blackrock))
                buffer[129] = 100;
            else
                buffer[129] = 0;
            if (OnSendMOTD != null)
            {
                OnSendMOTD(this, buffer);
            }
            SendRaw(0, buffer);

        }

        public void SendUserMOTD()
        {
            byte[] buffer = new byte[130];
            // Random rand = new Random();
            if (UsingWom && (level.textures.enabled || level.motd == "texture") && group.Permission >= level.textures.LowestRank.Permission) { StringFormat(Server.name, 64).CopyTo(buffer, 1); StringFormat("&0cfg=" + (IsLocalIpAddress(ip) ? ip : Server.IP) + ":" + Server.port + "/" + level.name, 64).CopyTo(buffer, 65); }
            if (level.motd == "ignore")
            {
                StringFormat(Server.name, 64).CopyTo(buffer, 1);
                if (!String.IsNullOrEmpty(group.MOTD)) StringFormat(group.MOTD, 64).CopyTo(buffer, 65);
                else StringFormat(Server.motd, 64).CopyTo(buffer, 65);
            }

            else StringFormat(level.motd, 128).CopyTo(buffer, 1);

            if (Block.canPlace(this.group.Permission, Block.blackrock))
                buffer[129] = 100;
            else
                buffer[129] = 0;
            SendRaw(0, buffer);
        }
        public void SendMap()
        {
            if (level.blocks == null) return;
            try
            {
                byte[] buffer = new byte[level.blocks.Length + 4];
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder(level.blocks.Length)).CopyTo(buffer, 0);
                //ushort xx; ushort yy; ushort zz;
                for (int i = 0; i < level.blocks.Length; ++i)
                {
                    if (extension)
                    {
                        buffer[4 + i] = (byte)Block.Convert(level.blocks[i]);
                    }
                    else
                    {
                        //Fallback
                        buffer[4 + i] = (byte)Block.Convert(Block.ConvertCPE(level.blocks[i]));
                    }
                }
                SendRaw(OpCode.MapBegin);
                buffer = buffer.GZip();
                int number = (int)Math.Ceiling(((double)buffer.Length) / 1024);
                for (int i = 1; buffer.Length > 0; ++i)
                {
                    short length = (short)Math.Min(buffer.Length, 1024);
                    byte[] send = new byte[1027];
                    HTNO(length).CopyTo(send, 0);
                    Buffer.BlockCopy(buffer, 0, send, 2, length);
                    byte[] tempbuffer = new byte[buffer.Length - length];
                    Buffer.BlockCopy(buffer, length, tempbuffer, 0, buffer.Length - length);
                    buffer = tempbuffer;
                    send[1026] = (byte)(i * 100 / number);
                    //send[1026] = (byte)(100 - (i * 100 / number)); // Backwards progress lololol...
                    SendRaw(OpCode.MapChunk, send);
                    if (ip == "127.0.0.1") { }
                    else if (Server.updateTimer.Interval > 1000) Thread.Sleep(100);
                    else Thread.Sleep(10);
                }
                buffer = new byte[6];
                HTNO((short)level.width).CopyTo(buffer, 0);
                HTNO((short)level.depth).CopyTo(buffer, 2);
                HTNO((short)level.height).CopyTo(buffer, 4);

                SendRaw(OpCode.MapEnd, buffer);
                Loading = false;

                if (OnSendMap != null)
                    OnSendMap(this, buffer);
            }
            catch (Exception ex)
            {
                Command.all.Find("goto").Use(this, Server.mainLevel.name);
                SendMessage("There was an error sending the map data, you have been sent to the main level.");
                Server.ErrorLog(ex);
            }
            finally
            {
                //if (derp) SendMessage("Something went derp when sending the map data, you should return to the main level.");
                //DateTime start = DateTime.Now;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                //Server.s.Log((DateTime.Now - start).TotalMilliseconds.ToString()); // We dont want random numbers showing up do we?
            }
            if (HasExtension("EnvWeatherType"))
            {
                SendSetMapWeather(level.weather);
            }
        }
        public void SendSpawn(byte id, string name, ushort x, ushort y, ushort z, byte rotx, byte roty, string displayName, string skinName)
        {
            if (!HasExtension("ExtPlayerList", 2))
            {
                //pos = new ushort[3] { x, y, z }; // This could be remove and not effect the server :/
                //rot = new byte[2] { rotx, roty };
                name = name.TrimEnd('+');
                byte[] buffer = new byte[73]; buffer[0] = id;
                StringFormat(name, 64).CopyTo(buffer, 1);
                HTNO(x).CopyTo(buffer, 65);
                HTNO(y).CopyTo(buffer, 67);
                HTNO(z).CopyTo(buffer, 69);
                buffer[71] = rotx; buffer[72] = roty;
                SendRaw(OpCode.AddEntity, buffer);
            }
            else
            {
                byte[] buffer = new byte[137];
                buffer[0] = id;
                StringFormat(displayName, 64).CopyTo(buffer, 1);
                StringFormat(skinName, 64).CopyTo(buffer, 65);
                HTNO(x).CopyTo(buffer, 129);
                HTNO(y).CopyTo(buffer, 131);
                HTNO(z).CopyTo(buffer, 133);
                buffer[135] = rotx;
                buffer[136] = roty;
                SendRaw((OpCode)33, buffer);
            }

            if (HasExtension("ChangeModel"))
            {
                Player.players.ForEach(p =>
                {
                    if (p.level == this.level)
                        if (p == this) unchecked { SendChangeModel((byte)-1, model); }
                        else
                        {
                            SendChangeModel(p.id, p.model);
                            if (p.HasExtension("ChangeModel"))
                                p.SendChangeModel(this.id, model);
                        }
                });
            }
        }
        public void SendPos(byte id, ushort x, ushort y, ushort z, byte rotx, byte roty)
        {
            if (x < 0) x = 32;
            if (y < 0) y = 32;
            if (z < 0) z = 32;
            if (x > level.width * 32) x = (ushort)(level.width * 32 - 32);
            if (z > level.height * 32) z = (ushort)(level.height * 32 - 32);
            if (x > 32767) x = 32730;
            if (y > 32767) y = 32730;
            if (z > 32767) z = 32730;

            pos[0] = x; pos[1] = y; pos[2] = z;
            rot[0] = rotx; rot[1] = roty;

            /*
pos = new ushort[3] { x, y, z };
rot = new byte[2] { rotx, roty };*/
            byte[] buffer = new byte[9]; buffer[0] = id;
            HTNO(x).CopyTo(buffer, 1);
            HTNO(y).CopyTo(buffer, 3);
            HTNO(z).CopyTo(buffer, 5);
            buffer[7] = rotx; buffer[8] = roty;
            SendRaw(OpCode.Teleport, buffer);
        }
        // Update user type for weather or not they are opped
        public void SendUserType(bool op)
        {
            SendRaw(OpCode.SetPermission, op ? (byte)100 : (byte)0);
        }
        //TODO: Figure a way to SendPos without changing rotation
        public void SendDie(byte id) { SendRaw(OpCode.RemoveEntity, new byte[1] { id }); }
        public void SendBlockchange(ushort x, ushort y, ushort z, ushort type)
        {
            if (type == Block.air) { type = 0; }
            if (x < 0 || y < 0 || z < 0) return;
            if (type > Block.maxblocks)
            {
                this.SendMessage("The server was not able to detect your held block, please try again!");
                return;
            }
            if (x >= level.width || y >= level.depth || z >= level.height) return;

            byte[] buffer = new byte[7];
            HTNO(x).CopyTo(buffer, 0);
            HTNO(y).CopyTo(buffer, 2);
            HTNO(z).CopyTo(buffer, 4);
            if (extension == true)
            {
                buffer[6] = (byte)Block.Convert(type);
            }
            else
            {
                buffer[6] = (byte)Block.Convert(Block.ConvertCPE(type));
            }
            SendRaw(OpCode.SetBlockServer, buffer);
        }
        void SendKick(string message) { SendRaw(OpCode.Kick, StringFormat(message, 64)); }
        void SendPing() { /*pingDelay = 0; pingDelayTimer.Start();*/ SendRaw(OpCode.Ping); }

        public void SendExtInfo(short count)
        {
            byte[] buffer = new byte[66];
            StringFormat("Server software: " + Server.SoftwareNameVersioned, 64).CopyTo(buffer, 0);
            HTNO(count).CopyTo(buffer, 64);
            SendRaw(OpCode.ExtInfo, buffer);
        }
        public void SendExtEntry(string name, int version)
        {
            byte[] version_ = BitConverter.GetBytes(version);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(version_);
            byte[] buffer = new byte[68];
            StringFormat(name, 64).CopyTo(buffer, 0);
            version_.CopyTo(buffer, 64);
            SendRaw(OpCode.ExtEntry, buffer);
        }
        public void SendClickDistance(short distance)
        {
            byte[] buffer = new byte[2];
            HTNO(distance).CopyTo(buffer, 0);
            SendRaw(OpCode.SetClickDistance, buffer);
        }
        public void SendCustomBlockSupportLevel(byte level)
        {
            byte[] buffer = new byte[1];
            buffer[0] = level;
            SendRaw(OpCode.CustomBlockSupportLevel, buffer);
        }
        public void SendHoldThis(byte type, byte locked)
        { // if locked is on 1, then the player can't change their selected block.
            byte[] buffer = new byte[2];
            buffer[0] = type;
            buffer[1] = locked;
            SendRaw(OpCode.HoldThis, buffer);
        }
        public void SendTextHotKey(string label, string command, int keycode, byte mods)
        {
            byte[] buffer = new byte[133];
            StringFormat(label, 64).CopyTo(buffer, 0);
            StringFormat(command, 64).CopyTo(buffer, 64);
            BitConverter.GetBytes(keycode).CopyTo(buffer, 128);
            buffer[132] = mods;
            SendRaw(OpCode.SetTextHotKey, buffer);
        }
        public void SendExtAddPlayerName(short id, string name, Group grp, string displayname = "")
        {
            byte[] buffer = new byte[195];
            HTNO(id).CopyTo(buffer, 0);
            StringFormat(name, 64).CopyTo(buffer, 2);
            if (displayname == "") { displayname = name; }
            StringFormat(displayname, 64).CopyTo(buffer, 66);
            StringFormat(grp.color + grp.name.ToUpper() + "s:", 64).CopyTo(buffer, 130);
            buffer[194] = (byte)grp.Permission.GetHashCode();
            SendRaw(OpCode.ExtAddPlayerName, buffer);
        }

        public void SendExtAddEntity(byte id, string name, string displayname = "")
        {
            byte[] buffer = new byte[129];
            buffer[0] = id;
            StringFormat(name, 64).CopyTo(buffer, 1);
            if (displayname == "") { displayname = name; }
            StringFormat(displayname, 64).CopyTo(buffer, 65);
            SendRaw(OpCode.ExtAddEntity, buffer);
        }

        public void SendExtRemovePlayerName(short id)
        {
            byte[] buffer = new byte[2];
            HTNO(id).CopyTo(buffer, 0);
            SendRaw(OpCode.ExtRemovePlayerName, buffer);
        }
        public void SendEnvSetColor(byte type, short r, short g, short b)
        {
            byte[] buffer = new byte[7];
            buffer[0] = type;
            HTNO(r).CopyTo(buffer, 1);
            HTNO(g).CopyTo(buffer, 3);
            HTNO(b).CopyTo(buffer, 5);
            SendRaw(OpCode.EnvSetColor, buffer);
        }
        public void SendMakeSelection(byte id, string label, short smallx, short smally, short smallz, short bigx, short bigy, short bigz, short r, short g, short b, short opacity)
        {
            byte[] buffer = new byte[85];
            buffer[0] = id;
            StringFormat(label, 64).CopyTo(buffer, 1);
            HTNO(smallx).CopyTo(buffer, 65);
            HTNO(smally).CopyTo(buffer, 67);
            HTNO(smallz).CopyTo(buffer, 69);
            HTNO(bigx).CopyTo(buffer, 71);
            HTNO(bigy).CopyTo(buffer, 73);
            HTNO(bigz).CopyTo(buffer, 75);
            HTNO(r).CopyTo(buffer, 77);
            HTNO(g).CopyTo(buffer, 79);
            HTNO(b).CopyTo(buffer, 81);
            HTNO(opacity).CopyTo(buffer, 83);
            SendRaw(OpCode.MakeSelection, buffer);
        }
        public void SendDeleteSelection(byte id)
        {
            byte[] buffer = new byte[1];
            buffer[0] = id;
            SendRaw(OpCode.RemoveSelection, buffer);
        }
        public void SendSetBlockPermission(byte type, byte canplace, byte candelete)
        {
            byte[] buffer = new byte[3];
            buffer[0] = type;
            buffer[1] = canplace;
            buffer[2] = candelete;
            SendRaw(OpCode.SetBlockPermission, buffer);
        }
        public void SendChangeModel(byte id, string model)
        {
            if (!HasExtension("ChangeModel")) { return; }
            byte[] buffer = new byte[65];
            buffer[0] = id;
            StringFormat(model, 64).CopyTo(buffer, 1);
            SendRaw(OpCode.ChangeModel, buffer);
        }
        public void SendSetMapAppearance(string url, byte sideblock, byte edgeblock, short sidelevel)
        {
            byte[] buffer = new byte[68];
            StringFormat(url, 64).CopyTo(buffer, 0);
            buffer[64] = sideblock;
            buffer[65] = edgeblock;
            HTNO(sidelevel).CopyTo(buffer, 66);
            SendRaw(OpCode.EnvMapAppearance, buffer);
        }
        public void SendSetMapWeather(byte weather)
        { // 0 - sunny; 1 - raining; 2 - snowing
            byte[] buffer = new byte[1];
            buffer[0] = weather;
            SendRaw(OpCode.EnvWeatherType, buffer);
        }
        public void SendHackControl(byte allowflying, byte allownoclip, byte allowspeeding, byte allowrespawning, byte allowthirdperson, byte allowchangingweather, short maxjumpheight)
        {
            byte[] buffer = new byte[7];
            buffer[0] = allowflying;
            buffer[1] = allownoclip;
            buffer[2] = allowspeeding;
            buffer[3] = allowrespawning;
            buffer[4] = allowthirdperson;
            buffer[5] = allowchangingweather;
            HTNO(maxjumpheight).CopyTo(buffer, 6);
            SendRaw(OpCode.HackControl, buffer);
        }

        public void UpdatePosition()
        {

            //pingDelayTimer.Stop();

            // Shameless copy from JTE's Server
            byte changed = 0; //Denotes what has changed (x,y,z, rotation-x, rotation-y)
            // 0 = no change - never happens with this code.
            // 1 = position has changed
            // 2 = rotation has changed
            // 3 = position and rotation have changed
            // 4 = Teleport Required (maybe something to do with spawning)
            // 5 = Teleport Required + position has changed
            // 6 = Teleport Required + rotation has changed
            // 7 = Teleport Required + position and rotation has changed
            //NOTE: Players should NOT be teleporting this often. This is probably causing some problems.
            if (oldpos[0] != pos[0] || oldpos[1] != pos[1] || oldpos[2] != pos[2])
                changed |= 1;

            if (oldrot[0] != rot[0] || oldrot[1] != rot[1])
            {
                changed |= 2;
            }
            /*if (Math.Abs(pos[0] - basepos[0]) > 32 || Math.Abs(pos[1] - basepos[1]) > 32 || Math.Abs(pos[2] - basepos[2]) > 32)
changed |= 4;

if ((oldpos[0] == pos[0] && oldpos[1] == pos[1] && oldpos[2] == pos[2]) && (basepos[0] != pos[0] || basepos[1] != pos[1] || basepos[2] != pos[2]))
changed |= 4;*/
            if (Math.Abs(pos[0] - oldpos[0]) > 32 || Math.Abs(pos[1] - oldpos[1]) > 32 || Math.Abs(pos[2] - oldpos[2]) > 32)
                changed |= 4;
            if (changed == 0) { if (oldpos[0] != pos[0] || oldpos[1] != pos[1] || oldpos[2] != pos[2]) changed |= 1; }

            byte[] buffer = new byte[0]; OpCode msg = 0;
            if ((changed & 4) != 0)
            {
                msg = OpCode.Teleport; //Player teleport - used for spawning or moving too fast
                buffer = new byte[9]; buffer[0] = id;
                HTNO(pos[0]).CopyTo(buffer, 1);
                HTNO(pos[1]).CopyTo(buffer, 3);
                HTNO(pos[2]).CopyTo(buffer, 5);
                buffer[7] = rot[0];

                if (Server.flipHead || (this.flipHead && this.infected))
                    if (rot[1] > 64 && rot[1] < 192)
                        buffer[8] = rot[1];
                    else
                        buffer[8] = (byte)(rot[1] - (rot[1] - 128));
                else
                    buffer[8] = rot[1];

                //Realcode
                //buffer[8] = rot[1];
            }
            else if (changed == 1)
            {
                try
                {
                    msg = OpCode.Move; //Position update
                    buffer = new byte[4]; buffer[0] = id;
                    Buffer.BlockCopy(System.BitConverter.GetBytes((sbyte)(pos[0] - oldpos[0])), 0, buffer, 1, 1);
                    Buffer.BlockCopy(System.BitConverter.GetBytes((sbyte)(pos[1] - oldpos[1])), 0, buffer, 2, 1);
                    Buffer.BlockCopy(System.BitConverter.GetBytes((sbyte)(pos[2] - oldpos[2])), 0, buffer, 3, 1);
                }
                catch { }
            }
            else if (changed == 2)
            {
                msg = OpCode.Rotate; //Orientation update
                buffer = new byte[3]; buffer[0] = id;
                buffer[1] = rot[0];

                if (Server.flipHead || (this.flipHead && this.infected))
                    if (rot[1] > 64 && rot[1] < 192)
                        buffer[2] = rot[1];
                    else
                        buffer[2] = (byte)(rot[1] - (rot[1] - 128));
                else
                    buffer[2] = rot[1];

                //Realcode
                //buffer[2] = rot[1];
            }
            else if (changed == 3)
            {
                try
                {
                    msg = OpCode.MoveRotate; //Position and orientation update
                    buffer = new byte[6]; buffer[0] = id;
                    Buffer.BlockCopy(System.BitConverter.GetBytes((sbyte)(pos[0] - oldpos[0])), 0, buffer, 1, 1);
                    Buffer.BlockCopy(System.BitConverter.GetBytes((sbyte)(pos[1] - oldpos[1])), 0, buffer, 2, 1);
                    Buffer.BlockCopy(System.BitConverter.GetBytes((sbyte)(pos[2] - oldpos[2])), 0, buffer, 3, 1);
                    buffer[4] = rot[0];

                    if (Server.flipHead || (this.flipHead && this.infected))
                        if (rot[1] > 64 && rot[1] < 192)
                            buffer[5] = rot[1];
                        else
                            buffer[5] = (byte)(rot[1] - (rot[1] - 128));
                    else
                        buffer[5] = rot[1];

                    //Realcode
                    //buffer[5] = rot[1];
                }
                catch { }
            }

            oldpos = pos; oldrot = rot;
            if (changed != 0)
                try
                {
                    foreach (Player p in players)
                    {
                        if (p != this && p.level == level)
                        {
                            p.SendRaw(msg, buffer);
                        }
                    }
                }
                catch { }
        }
        #endregion
        #region == GLOBAL MESSAGES ==
        public static void GlobalBlockchange(Level level, int b, ushort type)
        {
            ushort x, y, z;
            level.IntToPos(b, out x, out y, out z);
            GlobalBlockchange(level, x, y, z, type);
        }
        public static void GlobalBlockchange(Level level, ushort x, ushort y, ushort z, ushort type)
        {
            players.ForEach(delegate (Player p) { if (p.level == level) { p.SendBlockchange(x, y, z, type); } });
        }

        // THIS IS NOT FOR SENDING GLOBAL MESSAGES!!! IT IS TO SEND A MESSAGE FROM A SPECIFIED PLAYER!!!!!!!!!!!!!!
        public static void GlobalChat(Player from, string message) { GlobalChat(from, message, true); }
        public static void GlobalChat(Player from, string message, bool showname)
        {
            if (from == null) return; // So we don't fucking derp the hell out!

            if (MessageHasBadColorCodes(from, message))
                return;

            if (Server.lava.HasPlayer(from) && Server.lava.HasVote(message.ToLower()))
            {
                if (Server.lava.AddVote(from, message.ToLower()))
                {
                    SendMessage(from, "Your vote for &5" + message.ToLower().UppercaseFirst() + Server.DefaultColor + " has been placed. Thanks!");
                    Server.lava.map.ChatLevelOps(from.name + " voted for &5" + message.ToLower().UppercaseFirst() + Server.DefaultColor + ".");
                    return;
                }
                else
                {
                    SendMessage(from, "&cYou already voted!");
                    return;
                }
            }

            if (Server.voting == true)
            {
                if (message.ToLower() == "yes" || message.ToLower() == "ye" || message.ToLower() == "y")
                {
                    if (!from.voted)
                    {
                        Server.YesVotes++;
                        SendMessage(from, c.red + "Thanks For Voting!");
                        from.voted = true;
                        return;
                    }
                    else if (!from.voice)
                    {
                        from.SendMessage("Chat moderation is on while voting is on!");
                        return;
                    }
                }
                else if (message.ToLower() == "no" || message.ToLower() == "n")
                {
                    if (!from.voted)
                    {
                        Server.NoVotes++;
                        SendMessage(from, c.red + "Thanks For Voting!");
                        from.voted = true;
                        return;
                    }
                    else if (!from.voice)
                    {
                        from.SendMessage("Chat moderation is on while voting is on!");
                        return;
                    }
                }
            }

            if (Server.votingforlevel == true)
            {
                if (message.ToLower() == "1" || message.ToLower() == "one")
                {
                    if (!from.voted)
                    {
                        Server.Level1Vote++;
                        SendMessage(from, c.red + "Thanks For Voting!");
                        from.voted = true;
                        return;
                    }
                    else if (!from.voice)
                    {
                        from.SendMessage("Chat moderation is on while voting is on!");
                        return;
                    }
                }
                else if (message.ToLower() == "2" || message.ToLower() == "two")
                {
                    if (!from.voted)
                    {
                        Server.Level2Vote++;
                        SendMessage(from, c.red + "Thanks For Voting!");
                        from.voted = true;
                        return;
                    }
                    else if (!from.voice)
                    {
                        from.SendMessage("Chat moderation is on while voting is on!");
                        return;
                    }
                }
                else if (message.ToLower() == "3" || message.ToLower() == "random" || message.ToLower() == "rand")
                {
                    if (!from.voted)
                    {
                        Server.Level3Vote++;
                        SendMessage(from, c.red + "Thanks For Voting!");
                        from.voted = true;
                        return;
                    }
                    else if (!from.voice)
                    {
                        from.SendMessage("Chat moderation is on while voting is on!");
                        return;
                    }
                }
                else if (!from.voice)
                {
                    from.SendMessage("Chat moderation is on while voting is on!");
                    return;
                }
            }

            if (showname)
            {
                String referee = "";
                if (Server.gameStatus != 0)
                {
                    referee = "&f(" + from.explevel.levelID + ") ";
                }
                if (from.referee)
                {
                    referee = "&f(" + from.explevel.levelID + ") " + c.green + "[Referee] ";
                }
                if (from.ironmanActivated)
                {
                    referee = c.lime + "[IRONMAN] ";
                }
                if (!from.isHoldingFlag)
                {
                    if (from.pteam == 1)
                    {
                        referee = c.blue + "<Blue> ";
                    }
                    if (from.pteam == 2)
                    {
                        referee = c.red + "<Red> ";
                    }
                }
                if (from.isHoldingFlag)
                {
                    referee = c.maroon + "<" + c.red + "F" + c.gold + "l" + c.yellow + "a" + c.lime + "g " + c.green + "C" + c.aqua + "a" + c.blue + "r" + c.navy + "r" + c.purple + "i" + c.silver + "e" + c.gray + "r" + c.black + "> " + referee;
                }
                if (ZombieGame.infectd.Contains(from))
                {
                    referee = "&f(" + from.explevel.levelID + ") " + c.red + " [Z]";
                }
                if (ZombieGame.alive.Contains(from))
                {
                    referee = "&f(" + from.explevel.levelID + ") " + c.green + " [H]";
                }
                message = referee + from.color + from.voicestring + from.color + from.prefix + from.DisplayName + ": &f" + message;
            }
            players.ForEach(delegate (Player p)
            {
                if (p.level.worldChat && p.Chatroom == null)
                {
                    if (p.ignoreglobal == false)
                    {
                        if (from != null)
                        {
                            if (!p.listignored.Contains(from.name))
                            {
                                Player.SendMessage(p, message);
                                return;
                            }
                            return;
                        }
                        Player.SendMessage(p, message);
                        return;
                    }
                    if (Server.globalignoreops == false)
                    {
                        if (from.group.Permission >= Server.opchatperm)
                        {
                            if (p.group.Permission < from.group.Permission)
                            {
                                Player.SendMessage(p, message);
                            }
                        }
                    }
                    if (from != null)
                    {
                        if (from == p)
                        {
                            Player.SendMessage(from, message);
                            return;
                        }
                    }
                }
            });

        }
        public static void GlobalChatLevel(Player from, string message, bool showname)
        {
            if (MessageHasBadColorCodes(from, message))
                return;

            if (showname)
            {
                message = "<Level>" + from.color + from.voicestring + from.color + from.prefix + from.name + ": &f" + message;
            }
            players.ForEach(delegate (Player p)
            {
                if (p.level == from.level && p.Chatroom == null)
                {
                    if (p.ignoreglobal == false)
                    {
                        if (from != null)
                        {
                            if (!p.listignored.Contains(from.name))
                            {
                                Player.SendMessage(p, Server.DefaultColor + message);
                                return;
                            }
                            return;
                        }
                        Player.SendMessage(p, Server.DefaultColor + message);
                        return;
                    }
                    if (Server.globalignoreops == false)
                    {
                        if (from.group.Permission >= Server.opchatperm)
                        {
                            if (p.group.Permission < from.group.Permission)
                            {
                                Player.SendMessage(p, Server.DefaultColor + message);
                            }
                        }
                    }
                    if (from != null)
                    {
                        if (from == p)
                        {
                            Player.SendMessage(from, Server.DefaultColor + message);
                            return;
                        }
                    }
                }
            });
        }
        public static void GlobalChatRoom(Player from, string message, bool showname)
        {
            if (MessageHasBadColorCodes(from, message))
                return;
            string oldmessage = message;
            if (showname)
            {
                message = "<GlobalChatRoom> " + from.color + from.voicestring + from.color + from.prefix + from.name + ": &f" + message;
            }
            players.ForEach(delegate (Player p)
            {
                if (p.Chatroom != null)
                {
                    if (p.ignoreglobal == false)
                    {
                        if (from != null)
                        {
                            if (!p.listignored.Contains(from.name))
                            {
                                Player.SendMessage(p, Server.DefaultColor + message);
                                return;
                            }
                            return;
                        }
                        Player.SendMessage(p, Server.DefaultColor + message);
                        return;
                    }
                    if (Server.globalignoreops == false)
                    {
                        if (from.group.Permission >= Server.opchatperm)
                        {
                            if (p.group.Permission < from.group.Permission)
                            {
                                Player.SendMessage(p, Server.DefaultColor + message);
                            }
                        }
                    }
                    if (from != null)
                    {
                        if (from == p)
                        {
                            Player.SendMessage(from, Server.DefaultColor + message);
                            return;
                        }
                    }
                }
            });
            Server.s.Log(oldmessage + "<GlobalChatRoom>" + from.prefix + from.name + message);
        }
        public static void ChatRoom(Player from, string message, bool showname, string chatroom)
        {
            if (MessageHasBadColorCodes(from, message))
                return;
            string oldmessage = message;
            string messageforspy = ("<ChatRoomSPY: " + chatroom + "> " + from.color + from.voicestring + from.color + from.prefix + from.name + ": &f" + message);
            if (showname)
            {
                message = "<ChatRoom: " + chatroom + "> " + from.color + from.voicestring + from.color + from.prefix + from.name + ": &f" + message;
            }
            players.ForEach(delegate (Player p)
            {
                if (p.Chatroom == chatroom)
                {
                    if (p.ignoreglobal == false)
                    {
                        if (from != null)
                        {
                            if (!p.listignored.Contains(from.name))
                            {
                                Player.SendMessage(p, Server.DefaultColor + message);
                                return;
                            }
                            return;
                        }
                        Player.SendMessage(p, Server.DefaultColor + message);
                        return;
                    }
                    if (Server.globalignoreops == false)
                    {
                        if (from.group.Permission >= Server.opchatperm)
                        {
                            if (p.group.Permission < from.group.Permission)
                            {
                                Player.SendMessage(p, Server.DefaultColor + message);
                            }
                        }
                    }
                    if (from != null)
                    {
                        if (from == p)
                        {
                            Player.SendMessage(from, Server.DefaultColor + message);
                            return;
                        }
                    }
                }
            });
            players.ForEach(delegate (Player p)
            {
                if (p.spyChatRooms.Contains(chatroom) && p.Chatroom != chatroom)
                {
                    if (p.ignoreglobal == false)
                    {
                        if (from != null)
                        {
                            if (!p.listignored.Contains(from.name))
                            {
                                Player.SendMessage(p, Server.DefaultColor + message);
                                return;
                            }
                            return;
                        }
                        Player.SendMessage(p, Server.DefaultColor + message);
                        return;
                    }
                    if (Server.globalignoreops == false)
                    {
                        if (from.group.Permission >= Server.opchatperm)
                        {
                            if (p.group.Permission < from.group.Permission)
                            {
                                Player.SendMessage(p, Server.DefaultColor + messageforspy);
                            }
                        }
                    }
                    if (from != null)
                    {
                        if (from == p)
                        {
                            Player.SendMessage(from, Server.DefaultColor + messageforspy);
                            return;
                        }
                    }
                }
            });
            Server.s.Log(oldmessage + "<ChatRoom" + chatroom + ">" + from.prefix + from.name + message);
        }


        public static bool MessageHasBadColorCodes(Player from, string message)
        {
            if (HasBadColorCodes(message))
            {
                SendMessage(from, "Message not sent. You have bad color codes.");
                return true;
            }
            return false;
        }

        public static bool HasBadColorCodes(string message)
        {


            string[] sections = message.Split(new[] { '&', '%' });
            for (int i = 0; i < sections.Length; i++)
            {

                if (String.IsNullOrEmpty(sections[i].Trim()) && i == 0)
                { //If it starts with a color code
                    continue;
                }

                if (String.IsNullOrEmpty(sections[i].Trim()) && i - 1 != sections.Length)
                { //If it ends with a color code
                    continue;
                }

                if (String.IsNullOrEmpty(sections[i]) && i - 1 != sections.Length)
                {
                    return true;
                }

                if (!IsValidColorChar(sections[i][0]))
                {
                    sections[i] = 'a' + sections[i].Substring(1);
                }
            }

            return false;
        }

        public static bool IsValidColorChar(char color)
        {
            return (color >= '0' && color <= '9') || (color >= 'a' && color <= 'f') || (color >= 'A' && color <= 'F');
        }

        public static bool HasBadColorCodesTwo(string message)
        {
            string[] split = message.Split('&');
            for (int i = 0; i < split.Length; i++)
            {
                string section = split[i];

                if (String.IsNullOrEmpty(section.Trim()))
                    return true;

                if (!IsValidColorChar(section[0]))
                    return true;

            }

            return false;
        }

        public static bool CommandHasBadColourCodes(Player who, string message)
        {
            string[] checkmessagesplit = message.Split(' ');
            bool lastendwithcolour = false;
            foreach (string s in checkmessagesplit)
            {
                s.Trim();
                if (s.StartsWith("%"))
                {
                    if (lastendwithcolour)
                    {
                        if (who != null)
                        {
                            who.SendMessage("Sorry, Your colour codes in this command were invalid (You cannot use 2 colour codes next to each other");
                            who.SendMessage("Command failed.");
                            Server.s.Log(who.name + " attempted to send a command with invalid colours codes (2 colour codes were next to each other)!");
                            GlobalMessageOps(who.color + who.name + " " + Server.DefaultColor + " attempted to send a command with invalid colours codes (2 colour codes were next to each other)!");
                        }
                        return true;
                    }
                    else if (s.Length == 2)
                    {
                        lastendwithcolour = true;
                    }
                }
                if (s.TrimEnd(Server.ColourCodesNoPercent).EndsWith("%"))
                {
                    lastendwithcolour = true;
                }
                else
                {
                    lastendwithcolour = false;
                }

            }
            return false;
        }

        public static string EscapeColours(string message)
        {
            try
            {
                int index = 1;
                StringBuilder sb = new StringBuilder();
                Regex r = new Regex("^[0-9a-f]$");
                foreach (char c in message)
                {
                    if (c == '%')
                    {
                        if (message.Length >= index)
                            sb.Append(r.IsMatch(message[index].ToString()) ? '&' : '%');
                        else
                            sb.Append('%');
                    }
                    else
                        sb.Append(c);
                    index++;
                }
                return sb.ToString();
            }
            catch
            {
                return message;
            }

        }

        public static void GlobalChatWorld(Player from, string message, bool showname)
        {
            if (showname)
            {
                message = "<World>" + from.color + from.voicestring + from.color + from.prefix + from.name + ": &f" + message;
            }
            players.ForEach(delegate (Player p)
            {
                if (p.level.worldChat && p.Chatroom == null)
                {
                    if (p.ignoreglobal == false)
                    {
                        if (from != null)
                        {
                            if (!p.listignored.Contains(from.name))
                            {
                                Player.SendMessage(p, Server.DefaultColor + message);
                                return;
                            }
                            return;
                        }
                        Player.SendMessage(p, Server.DefaultColor + message);
                        return;
                    }
                    if (Server.globalignoreops == false)
                    {
                        if (from.group.Permission >= Server.opchatperm)
                        {
                            if (p.group.Permission < from.group.Permission)
                            {
                                Player.SendMessage(p, Server.DefaultColor + message);
                            }
                        }
                    }
                    if (from != null)
                    {
                        if (from == p)
                        {
                            Player.SendMessage(from, Server.DefaultColor + message);
                            return;
                        }
                    }
                }
            });
        }
        public static void GlobalMessage(string message)
        {
            GlobalMessage(MessageType.Chat, message, false);
        }
        public static void GlobalMessage(MessageType type, string message, bool global)
        {
            if (!global)
                //message = message.Replace("%", "&");
                message = EscapeColours(message);
            players.ForEach(delegate (Player p)
            {
                if (p.level.worldChat && p.Chatroom == null && (!global || !p.muteGlobal))
                {
                    Player.SendMessage(p, type, message, !global);
                }
            });
        }
        public static void GlobalMessageLevel(Level l, string message)
        {
            players.ForEach(delegate (Player p) { if (p.level == l && p.Chatroom == null) Player.SendMessage(p, MessageType.Chat, message, true); });
        }

        public static void GlobalMessageLevel(Level l, MessageType type, string message)
        {
            players.ForEach(delegate (Player p) { if (p.level == l && p.Chatroom == null) Player.SendMessage(p, type, message, true); });
        }

        public static void GlobalMessageOps(string message)
        {
            try
            {
                players.ForEach(delegate (Player p)
                {
                    if (p.group.Permission >= Server.opchatperm || p.isStaff)
                    { //START
                        Player.SendMessage(p, message);
                    }
                });

            }
            catch { Server.s.Log("Error occured with Op Chat"); }
        }
        public static void GlobalMessageAdmins(string message)
        {
            try
            {
                players.ForEach(delegate (Player p)
                {
                    if (p.group.Permission >= Server.adminchatperm || p.isStaff)
                    {
                        Player.SendMessage(p, message);
                    }
                });

            }
            catch { Server.s.Log("Error occured with Admin Chat"); }
        }

        /* public static void GlobalMessageTeam(string message, string team)
         {
             try
             {
                 players.ForEach(delegate(Player p)
                 {
                     if (Server.pctf.getTeam(p) == team || Server.devs.Contains(p.name.ToLower()) || p.referee == true)
                     {
                         if (team == "red")
                             Player.SendMessage(p, c.red + "To RED Team &f-" + message);
                         else if (team == "blue")
                             Player.SendMessage(p, c.blue + "To BLUE Team &f-" + message);
                         else { }
                     }
                 });

             }
             catch { Server.s.Log("Error occured with Team Chat"); }
         } */

        public static void GlobalSpawn(Player from, ushort x, ushort y, ushort z, byte rotx, byte roty, bool self, string possession = "")
        {
            players.ForEach(delegate (Player p)
            {
                if (p.Loading && p != from) { return; }
                if (p.level != from.level || (from.hidden && !self)) { return; }
                if (p != from)
                {
                    if (Server.ZombieModeOn && !p.aka)
                    {
                        if (from.infected)
                        {
                            if (Server.ZombieName != "")
                                p.SendSpawn(from.id, c.red + Server.ZombieName + possession, x, y, z, rotx, roty, from.DisplayName, from.SkinName);
                            else
                                p.SendSpawn(from.id, c.red + from.name + possession, x, y, z, rotx, roty, from.DisplayName, from.SkinName);
                            return;
                        }
                        else if (from.referee)
                        {
                            return;
                        }
                        else
                        {
                            p.SendSpawn(from.id, from.color + from.name + possession, x, y, z, rotx, roty, from.DisplayName, from.SkinName);
                            return;
                        }
                    }
                    else
                    {
                        p.SendSpawn(from.id, from.color + from.name + possession, x, y, z, rotx, roty, from.DisplayName, from.SkinName);
                    }
                }
                else if (self)
                {
                    if (!p.ignorePermission)
                    {
                        p.pos = new ushort[3] { x, y, z }; p.rot = new byte[2] { rotx, roty };
                        p.oldpos = p.pos; p.oldrot = p.rot;
                        unchecked { p.SendSpawn((byte)-1, from.color + from.name + possession, x, y, z, rotx, roty, from.DisplayName, from.SkinName); }
                    }
                }
            });
        }
        public static void GlobalDie(Player from, bool self)
        {
            players.ForEach(delegate (Player p)
            {
                if (p.level != from.level || (from.hidden && !self)) { return; }
                if (p != from) { p.SendDie(from.id); }
                else if (self) { p.SendDie(255); }
            });
        }

        public bool MarkPossessed(string marker = "")
        {
            if (marker != "")
            {
                Player controller = Player.Find(marker);
                if (controller == null)
                {
                    return false;
                }
                marker = " (" + controller.color + controller.name + color + ")";
            }
            GlobalDie(this, true);
            GlobalSpawn(this, pos[0], pos[1], pos[2], rot[0], rot[1], true, marker);
            return true;
        }

        public static void GlobalUpdate() { players.ForEach(delegate (Player p) { if (!p.hidden) { p.UpdatePosition(); } }); }
        #endregion
        #region == DISCONNECTING ==
        public void Disconnect() { leftGame(); }
        public void Kick(string kickString) { leftGame(kickString); }

        internal void CloseSocket()
        {
            // Try to close the socket.
            // Sometimes its already closed so these lines will cause an error
            // We just trap them and hide them from view :P
            try
            {
                // Close the damn socket connection!
                socket.Shutdown(SocketShutdown.Both);
#if DEBUG
                Server.s.Log(this.name ?? this.ip + "disconnected");
#endif
            }
            catch (Exception)
            {
#if DEBUG
                Exception ex = new Exception("Failed to shutdown socket for " + this.name ?? this.ip);
                Server.ErrorLog(ex);
#endif
            }

            try
            {
                socket.Close();
#if DEBUG
                Server.s.Log(this.name ?? this.ip + "disconnected");
#endif
            }
            catch (Exception)
            {
#if DEBUG
                Exception ex = new Exception("Failed to close socket for " + this.name ?? this.ip);
                Server.ErrorLog(ex);
#endif
            }
        }

        public void leftGame(string kickString = "", bool skip = false)
        {

            OnPlayerDisconnectEvent.Call(this, kickString);

            //Umm...fixed?
            if (name == "")
            {
                if (socket != null)
                    CloseSocket();
                if (connections.Contains(this))
                    connections.Remove(this);
                SaveUndo();
                disconnected = true;
                return;
            }
            ////If player has been found in the reviewlist he will be removed
            bool leavetest = false;
            foreach (string testwho2 in Server.reviewlist)
            {
                if (testwho2 == name)
                {
                    leavetest = true;
                }
            }
            if (leavetest)
            {
                Server.reviewlist.Remove(name);
            }
            try
            {

                if (disconnected)
                {
                    this.CloseSocket();
                    if (connections.Contains(this))
                        connections.Remove(this);
                    return;
                }
                // FlyBuffer.Clear();
                disconnected = true;
                pingTimer.Stop();
                pingTimer.Dispose();
                if (File.Exists("ranks/ignore/" + this.name + ".txt"))
                {
                    try
                    {
                        File.WriteAllLines("ranks/ignore/" + this.name + ".txt", this.listignored.ToArray());
                    }
                    catch
                    {
                        Server.s.Log("Failed to save ignored list for player: " + this.name);
                    }
                }
                if (File.Exists("ranks/ignore/GlobalIgnore.xml"))
                {
                    try
                    {
                        File.WriteAllLines("ranks/ignore/GlobalIgnore.xml", globalignores.ToArray());
                    }
                    catch
                    {
                        Server.s.Log("failed to save global ignore list!");
                    }
                }
                afkTimer.Stop();
                afkTimer.Dispose();
                muteTimer.Stop();
                muteTimer.Dispose();
                timespent.Stop();
                timespent.Dispose();
                afkCount = 0;
                afkStart = DateTime.Now;

                if (Server.afkset.Contains(name)) Server.afkset.Remove(name);

                if (kickString == "") kickString = "Disconnected.";

                SendKick(kickString);


                if (loggedIn)
                {
                    isFlying = false;
                    aiming = false;


                    if (CountdownGame.players.Contains(this))
                    {
                        if (CountdownGame.playersleftlist.Contains(this))
                        {
                            CountdownGame.PlayerLeft(this);
                        }
                        CountdownGame.players.Remove(this);
                    }

                    TntWarsGame tntwarsgame = TntWarsGame.GetTntWarsGame(this);
                    if (tntwarsgame != null)
                    {
                        tntwarsgame.Players.Remove(tntwarsgame.FindPlayer(this));
                        tntwarsgame.SendAllPlayersMessage("TNT Wars: " + color + name + Server.DefaultColor + " has left TNT Wars!");
                    }

                    GlobalDie(this, false);
                    if (kickString == "Disconnected." || kickString.IndexOf("Server shutdown") != -1 || kickString == Server.customShutdownMessage)
                    {
                        if (!Directory.Exists("text/logout"))
                        {
                            Directory.CreateDirectory("text/logout");
                        }
                        if (!File.Exists("text/logout/" + name + ".txt"))
                        {
                            File.WriteAllText("text/logout/" + name + ".txt", "Disconnected.");
                        }
                        if (!hidden)
                        {
                            string leavem = "&c- " + color + prefix + name + Server.DefaultColor + " " + File.ReadAllText("text/logout/" + name + ".txt");
                            if ((Server.guestLeaveNotify && this.group.Permission <= LevelPermission.Guest) || this.group.Permission > LevelPermission.Guest)
                            {
                                Player.players.ForEach(delegate (Player p1)
                                {
                                    if (p1.UsingWom)
                                    {
                                        byte[] buffer = new byte[65];
                                        Player.StringFormat("^detail.user.part=" + color + name + c.white, 64).CopyTo(buffer, 1);
                                        p1.SendRaw(OpCode.Message, buffer);
                                        buffer = null;
                                        Player.SendMessage(p1, leavem);
                                    }
                                    else
                                        Player.SendMessage(p1, leavem);
                                });
                            }
                        }
                        //IRCBot.Say(name + " left the game.");
                        Server.s.Log(name + " disconnected.");
                    }
                    else
                    {
                        totalKicked++;
                        GlobalChat(this, "&c- " + color + prefix + name + Server.DefaultColor + " kicked (" + kickString + Server.DefaultColor + ").", false);
                        //IRCBot.Say(name + " kicked (" + kickString + ").");
                        Server.s.Log(name + " kicked (" + kickString + ").");
                    }

                    try { save(); }
                    catch (Exception e) { Server.ErrorLog(e); }
                    players.Remove(this);
                    players.ForEach(delegate (Player p)
                    {
                        if (p != this && p.extension)
                        {
                            p.SendExtRemovePlayerName(this.id);
                        }
                    });
                    Server.s.PlayerListUpdate();
                    try
                    {
                        left.Add(this.name.ToLower(), this.ip);
                    }
                    catch (Exception)
                    {
                        //Server.ErrorLog(e);
                    }

                    /*if (Server.AutoLoad && level.unload)
{

foreach (Player pl in Player.players)
if (pl.level == level) hasplayers = true;
if (!level.name.Contains("Museum " + Server.DefaultColor) && hasplayers == false)
{
level.Unload();
}
}*/

                    if (Server.AutoLoad && level.unload && !level.name.Contains("Museum " + Server.DefaultColor) && IsAloneOnCurrentLevel())
                        level.Unload(true);

                    if (PlayerDisconnect != null)
                        PlayerDisconnect(this, kickString);

                    this.Dispose();
                }
                else
                {
                    connections.Remove(this);

                    Server.s.Log(ip + " disconnected.");
                }
                if (Server.zombie.GameInProgess())
                {
                    try
                    {
                        if (ZombieGame.infectd.Contains(this))
                        {
                            ZombieGame.infectd.Remove(this);
                        }
                        if (ZombieGame.alive.Contains(this))
                        {
                            ZombieGame.alive.Remove(this);
                        }
                    }
                    catch { }
                }
                Server.zombie.InfectedPlayerDC();
                //       Server.pctf.PlayerDC(this);

            }
            catch (Exception e) { Server.ErrorLog(e); }
            finally
            {
                CloseSocket();
            }
        }

        public void SaveUndo()
        {
            SaveUndo(this);
        }
        public static void SaveUndo(Player p)
        {
            if (p == null || p.UndoBuffer == null || p.UndoBuffer.Count < 1) return;
            try
            {
                if (!Directory.Exists("extra/undo")) Directory.CreateDirectory("extra/undo");
                if (!Directory.Exists("extra/undoPrevious")) Directory.CreateDirectory("extra/undoPrevious");
                DirectoryInfo di = new DirectoryInfo("extra/undo");
                if (di.GetDirectories("*").Length >= Server.totalUndo)
                {
                    Directory.Delete("extra/undoPrevious", true);
                    Directory.Move("extra/undo", "extra/undoPrevious");
                    Directory.CreateDirectory("extra/undo");
                }

                if (!Directory.Exists("extra/undo/" + p.name.ToLower())) Directory.CreateDirectory("extra/undo/" + p.name.ToLower());
                di = new DirectoryInfo("extra/undo/" + p.name.ToLower());
                int number = di.GetFiles("*.undo").Length;
                File.Create("extra/undo/" + p.name.ToLower() + "/" + number + ".undo").Dispose();
                using (StreamWriter w = File.CreateText("extra/undo/" + p.name.ToLower() + "/" + number + ".undo"))
                {
                    foreach (UndoPos uP in p.UndoBuffer.ToList())
                    {
                        w.Write(uP.mapName + " " +
                                uP.x + " " + uP.y + " " + uP.z + " " +
                                uP.timePlaced.ToString(CultureInfo.InvariantCulture).Replace(' ', '&') + " " +
                                uP.type + " " + uP.newtype + " ");
                    }
                }
            }
            catch (Exception e) { Server.s.Log("Error saving undo data for " + p.name + "!"); Server.ErrorLog(e); }
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
            if (connections.Contains(this)) connections.Remove(this);
            Extras.Clear();
            CopyBuffer.Clear();
            RedoBuffer.Clear();
            UndoBuffer.Clear();
            spamBlockLog.Clear();
            //spamChatLog.Clear();
            spyChatRooms.Clear();
            /*try
{
//this.commThread.Abort();
}
catch { }*/
        }
        //fixed undo code
        public bool IsAloneOnCurrentLevel()
        {
            return players.All(pl => pl.level != level || pl == this);
        }

        #endregion
        #region == CHECKING ==
        public static List<Player> GetPlayers() { return new List<Player>(players); }
        public static bool Exists(string name)
        {
            foreach (Player p in players) { if (p.name.ToLower() == name.ToLower()) { return true; } }
            return false;
        }
        public static bool Exists(byte id)
        {
            foreach (Player p in players) { if (p.id == id) { return true; } }
            return false;
        }
        public static Player Find(string name)
        {
            List<Player> tempList = new List<Player>();
            tempList.AddRange(players);
            Player tempPlayer = null; bool returnNull = false;

            foreach (Player p in tempList)
            {
                if (p.name.ToLower() == name.ToLower()) return p;
                if (p.name.ToLower().IndexOf(name.ToLower()) != -1)
                {
                    if (tempPlayer == null) tempPlayer = p;
                    else returnNull = true;
                }
            }

            if (returnNull == true) return null;
            if (tempPlayer != null) return tempPlayer;
            return null;
        }
        public static Group GetGroup(string name)
        {
            return Group.findPlayerGroup(name);
        }
        public static string GetColor(string name)
        {
            return GetGroup(name).color;
        }
        /*     public static OfflinePlayer FindOffline(string name)
             {
                 OfflinePlayer offPlayer = new OfflinePlayer("", "", "", "", 0);
                 Database.AddParams("@Name", name);
                 using (DataTable playerDB = Database.fillData("SELECT * FROM Players WHERE Name = @Name"))
                 {
                     if (playerDB.Rows.Count == 0)
                         return offPlayer;
                     else
                     {
                         offPlayer.name = playerDB.Rows[0]["Name"].ToString().Trim();
                         offPlayer.title = playerDB.Rows[0]["Title"].ToString().Trim();
                         offPlayer.titleColor = c.Parse(playerDB.Rows[0]["title_color"].ToString().Trim());
                         offPlayer.color = c.Parse(playerDB.Rows[0]["color"].ToString().Trim());
                         offPlayer.money = int.Parse(playerDB.Rows[0]["Money"].ToString());
                         if (offPlayer.color == "") { offPlayer.color = GetGroup(offPlayer.name).color; }
                     }
                 }
                 return offPlayer;
             }*/
        #endregion
        #region == OTHER ==
        static byte FreeId()
        {
            /*
for (byte i = 0; i < 255; i++)
{
foreach (Player p in players)
{
if (p.id == i) { goto Next; }
} return i;
Next: continue;
} unchecked { return (byte)-1; }*/

            for (ushort i = 0; i < Block.maxblocks; i++)
            {
                bool used = players.Any(p => p.id == i);

                if (!used)
                    return (byte)i;
            }
            return (byte)1;
        }
        public static byte[] StringFormat(string str, int size)
        {
            byte[] bytes = new byte[size];
            bytes = enc.GetBytes(str.PadRight(size).Substring(0, size));
            return bytes;
        }

        // TODO: Optimize this using a StringBuilder
        static List<string> Wordwrap(string message)
        {
            List<string> lines = new List<string>();
            message = Regex.Replace(message, @"(&[0-9a-f])+(&[0-9a-f])", "$2");
            message = Regex.Replace(message, @"(&[0-9a-f])+$", "");

            int limit = 64; string color = "";
            while (message.Length > 0)
            {
                //if (Regex.IsMatch(message, "&a")) break;

                if (lines.Count > 0)
                {
                    if (message[0].ToString() == "&")
                        message = "> " + message.Trim();
                    else
                        message = "> " + color + message.Trim();
                }

                if (message.IndexOf("&") == message.IndexOf("&", message.IndexOf("&") + 1) - 2)
                    message = message.Remove(message.IndexOf("&"), 2);

                if (message.Length <= limit) { lines.Add(message); break; }
                for (int i = limit - 1; i > limit - 20; --i)
                    if (message[i] == ' ')
                    {
                        lines.Add(message.Substring(0, i));
                        goto Next;
                    }

                retry:
                if (message.Length == 0 || limit == 0) { return lines; }

                try
                {
                    if (message.Substring(limit - 2, 1) == "&" || message.Substring(limit - 1, 1) == "&")
                    {
                        message = message.Remove(limit - 2, 1);
                        limit -= 2;
                        goto retry;
                    }
                    else if (message[limit - 1] < 32 || message[limit - 1] > 127)
                    {
                        message = message.Remove(limit - 1, 1);
                        limit -= 1;
                        //goto retry;
                    }
                }
                catch { return lines; }
                lines.Add(message.Substring(0, limit));

            Next: message = message.Substring(lines[lines.Count - 1].Length);
                if (lines.Count == 1) limit = 60;

                int index = lines[lines.Count - 1].LastIndexOf('&');
                if (index != -1)
                {
                    if (index < lines[lines.Count - 1].Length - 1)
                    {
                        char next = lines[lines.Count - 1][index + 1];
                        if ("0123456789abcdef".IndexOf(next) != -1) { color = "&" + next; }
                        if (index == lines[lines.Count - 1].Length - 1)
                        {
                            lines[lines.Count - 1] = lines[lines.Count - 1].Substring(0, lines[lines.Count - 1].Length - 2);
                        }
                    }
                    else if (message.Length != 0)
                    {
                        char next = message[0];
                        if ("0123456789abcdef".IndexOf(next) != -1)
                        {
                            color = "&" + next;
                        }
                        lines[lines.Count - 1] = lines[lines.Count - 1].Substring(0, lines[lines.Count - 1].Length - 1);
                        message = message.Substring(1);
                    }
                }
            }
            char[] temp;
            for (int i = 0; i < lines.Count; i++) // Gotta do it the old fashioned way...
            {
                temp = lines[i].ToCharArray();
                if (temp[temp.Length - 2] == '%' || temp[temp.Length - 2] == '&')
                {
                    temp[temp.Length - 1] = ' ';
                    temp[temp.Length - 2] = ' ';
                }
                StringBuilder message1 = new StringBuilder();
                message1.Append(temp);
                lines[i] = message1.ToString();
            }
            return lines;
        }
        public static bool ValidName(string name, Player p = null)
        {
            string allowedchars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890._@+";
            if (p != null && p.Mojangaccount) allowedchars += "-";
            return name.All(ch => allowedchars.IndexOf(ch) != -1);
        }

        public static int GetBannedCount()
        {
            try
            {
                return File.ReadAllLines("ranks/banned.txt").Length;
            }
            catch/* (Exception ex)*/
            {
                return 0;
            }
        }

        public static bool CommandProtected(string cmd, string message)
        {
            string foundName = "";
            Player who = null;
            bool self = false;
            if (Server.ProtectOver.Contains(cmd))
                switch (cmd)
                {
                    //case "demote":
                    case "freeze":
                    case "impersonate":
                    //case "kick":
                    case "kickban":
                    case "mute":
                    case "possess":
                    //case "promote":
                    case "sendcmd":
                    case "tempban":
                    case "uban":
                    case "voice":
                    case "xban":
                        //case "unban":
                        //case "xundo":
                        if (message.Split().Length > 0)
                        {
                            who = Find(message.Split()[0]);
                            foundName = who != null ? who.name : message.Split()[0];
                        }
                        break;
                    /*case "banip": //this one is hard coded into CmdBanip.cs
                        break;*/
                    case "ban":
                    case "joker":
                        if (message.Split().Length > 0)
                        {
                            try
                            {
                                who = message.StartsWith("@") || message.StartsWith("#") ? Find(message.Split()[0].Substring(1)) : Find(message.Split()[0]);
                            }
                            catch (ArgumentOutOfRangeException) { who = null; }
                            foundName = who != null ? who.name : message.Split()[0];
                        }
                        break;
                    case "lockdown":
                        if (message.Split().Length > 1 && message.Split()[0].ToLower() == "player")
                        {
                            who = Find(message.Split()[1]);
                            foundName = who != null ? who.name : message.Split()[1];
                        }
                        break;
                    case "jail":
                        if (message.Split().Length > 0 && message.Split()[0].ToLower() != "set")
                        {
                            who = Find(message.Split()[0]);
                            foundName = who != null ? who.name : message.Split()[0];
                        }
                        break;
                    case "ignore":
                        List<string> badlist = new List<string>();
                        badlist.Add("all"); badlist.Add("global"); badlist.Add("list");
                        if (message.Split().Length > 0 && badlist.Contains(message.Split()[0].ToLower()))
                        {
                            who = Find(message.Split()[0]);
                            foundName = who != null ? who.name : message.Split()[0];
                        }
                        badlist = null;
                        break;
                    default:
                        break;
                }
            foundName = foundName.ToLower();
            if (who != null && foundName == who.name.ToLower()) { self = true; }
            try
            {
                if (Server.forgeProtection == ForgeProtection.Mod)
                    return (Server.Mods.Contains(foundName) || Server.Devs.Contains(foundName)) && !self;
                if (Server.forgeProtection == ForgeProtection.Dev)
                    return Server.Devs.Contains(foundName) && !self;
            }
            catch { }
            return false;
        }
        #endregion
        #region == Host <> Network ==
        public static byte[] HTNO(ushort x)
        {
            byte[] y = BitConverter.GetBytes(x); Array.Reverse(y); return y;
        }
        public static ushort NTHO(byte[] x, int offset)
        {
            byte[] y = new byte[2];
            Buffer.BlockCopy(x, offset, y, 0, 2); Array.Reverse(y);
            return BitConverter.ToUInt16(y, 0);
        }
        public static byte[] HTNO(short x)
        {
            byte[] y = BitConverter.GetBytes(x); Array.Reverse(y); return y;
        }
        #endregion

        bool CheckBlockSpam()
        {
            if (spamBlockLog.Count >= spamBlockCount)
            {
                DateTime oldestTime = spamBlockLog.Dequeue();
                double spamTimer = DateTime.Now.Subtract(oldestTime).TotalSeconds;
                if (spamTimer < spamBlockTimer && !ignoreGrief)
                {
                    this.Kick("You were kicked by antigrief system. Slow down.");
                    SendMessage(c.red + name + " was kicked for suspected griefing.");
                    Server.s.Log(name + " was kicked for block spam (" + spamBlockCount + " blocks in " + spamTimer + " seconds)");
                    return true;
                }
            }
            spamBlockLog.Enqueue(DateTime.Now);
            return false;
        }

        #region getters
        public ushort[] footLocation
        {
            get
            {
                return getLoc(false);
            }
        }
        public ushort[] headLocation
        {
            get
            {
                return getLoc(true);
            }
        }

        public ushort[] getLoc(bool head)
        {
            ushort[] myPos = pos;
            myPos[0] /= 32;
            if (head) myPos[1] = (ushort)((myPos[1] + 4) / 32);
            else myPos[1] = (ushort)((myPos[1] + 4) / 32 - 1);
            myPos[2] /= 32;
            return myPos;
        }

        public void setLoc(ushort[] myPos)
        {
            myPos[0] *= 32;
            myPos[1] *= 32;
            myPos[2] *= 32;
            unchecked { SendPos((byte)-1, myPos[0], myPos[1], myPos[2], rot[0], rot[1]); }
        }

        #endregion

        public static bool IPInPrivateRange(string ip)
        {
            //range of 172.16.0.0 - 172.31.255.255
            if (ip.StartsWith("172.") && (int.Parse(ip.Split('.')[1]) >= 16 && int.Parse(ip.Split('.')[1]) <= 31))
                return true;
            return IPAddress.IsLoopback(IPAddress.Parse(ip)) || ip.StartsWith("192.168.") || ip.StartsWith("10.");
            //return IsLocalIpAddress(ip);
        }

        public string ResolveExternalIP(string ip)
        {
            HTTPGet req = new HTTPGet();
            req.Request("http://checkip.dyndns.org");
            string[] a1 = req.ResponseBody.Split(':');
            string a2 = a1[1].Substring(1);
            string[] a3 = a2.Split('<');
            return a3[0];
        }

        public static bool IsLocalIpAddress(string host)
        {
            try
            { // get host IP addresses
                IPAddress[] hostIPs = Dns.GetHostAddresses(host);
                // get local IP addresses
                IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

                // test if any host IP equals to any local IP or to localhost
                foreach (IPAddress hostIP in hostIPs)
                {
                    // is localhost
                    if (IPAddress.IsLoopback(hostIP)) return true;
                    // is local address
                    foreach (IPAddress localIP in localIPs)
                    {
                        if (hostIP.Equals(localIP)) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public class Waypoint
        {
            public class WP
            {
                public ushort x;
                public ushort y;
                public ushort z;
                public byte rotx;
                public byte roty;
                public string name;
                public string lvlname;
            }
            public static WP Find(string name, Player p)
            {
                WP wpfound = null;
                bool found = false;
                foreach (WP wp in p.Waypoints)
                {
                    if (wp.name.ToLower() == name.ToLower())
                    {
                        wpfound = wp;
                        found = true;
                    }
                }
                if (found) { return wpfound; }
                else { return null; }
            }
            public static void Goto(string waypoint, Player p)
            {
                if (!Exists(waypoint, p)) return;
                WP wp = Find(waypoint, p);
                Level lvl = Level.Find(wp.lvlname);
                if (wp == null) return;
                if (lvl != null)
                {
                    if (p.level != lvl)
                    {
                        Command.all.Find("goto").Use(p, lvl.name);
                        while (p.Loading) { Thread.Sleep(250); }
                    }
                    unchecked { p.SendPos((byte)-1, wp.x, wp.y, wp.z, wp.rotx, wp.roty); }
                    Player.SendMessage(p, "Sent you to waypoint");
                }
                else { Player.SendMessage(p, "The map that that waypoint is on isn't loaded right now (" + wp.lvlname + ")"); return; }
            }

            public static void Create(string waypoint, Player p)
            {
                Player.Waypoint.WP wp = new Player.Waypoint.WP();
                {
                    wp.x = p.pos[0];
                    wp.y = p.pos[1];
                    wp.z = p.pos[2];
                    wp.rotx = p.rot[0];
                    wp.roty = p.rot[1];
                    wp.name = waypoint;
                    wp.lvlname = p.level.name;
                }
                p.Waypoints.Add(wp);
                Save();
            }

            public static void Update(string waypoint, Player p)
            {
                WP wp = Find(waypoint, p);
                p.Waypoints.Remove(wp);
                {
                    wp.x = p.pos[0];
                    wp.y = p.pos[1];
                    wp.z = p.pos[2];
                    wp.rotx = p.rot[0];
                    wp.roty = p.rot[1];
                    wp.name = waypoint;
                    wp.lvlname = p.level.name;
                }
                p.Waypoints.Add(wp);
                Save();
            }

            public static void Remove(string waypoint, Player p)
            {
                WP wp = Find(waypoint, p);
                p.Waypoints.Remove(wp);
                Save();
            }

            public static bool Exists(string waypoint, Player p)
            {
                bool exists = false;
                foreach (WP wp in p.Waypoints)
                {
                    if (wp.name.ToLower() == waypoint.ToLower())
                    {
                        exists = true;
                    }
                }
                return exists;
            }

            public static void Load(Player p)
            {
                if (File.Exists("extra/Waypoints/" + p.name + ".save"))
                {
                    using (StreamReader SR = new StreamReader("extra/Waypoints/" + p.name + ".save"))
                    {
                        bool failed = false;
                        string line;
                        while (SR.EndOfStream == false)
                        {
                            line = SR.ReadLine().ToLower().Trim();
                            if (!line.StartsWith("#") && line.Contains(":"))
                            {
                                failed = false;
                                string[] LINE = line.ToLower().Split(':');
                                WP wp = new WP();
                                try
                                {
                                    wp.name = LINE[0];
                                    wp.lvlname = LINE[1];
                                    wp.x = ushort.Parse(LINE[2]);
                                    wp.y = ushort.Parse(LINE[3]);
                                    wp.z = ushort.Parse(LINE[4]);
                                    wp.rotx = byte.Parse(LINE[5]);
                                    wp.roty = byte.Parse(LINE[6]);
                                }
                                catch
                                {
                                    Server.s.Log("Couldn't load a Waypoint!");
                                    failed = true;
                                }
                                if (failed == false)
                                {
                                    p.Waypoints.Add(wp);
                                }
                            }
                        }
                        SR.Dispose();
                    }
                }
            }

            public static void Save()
            {
                foreach (Player p in Player.players)
                {
                    if (p.Waypoints.Count >= 1)
                    {
                        using (StreamWriter SW = new StreamWriter("extra/Waypoints/" + p.name + ".save"))
                        {
                            foreach (WP wp in p.Waypoints)
                            {
                                SW.WriteLine(wp.name + ":" + wp.lvlname + ":" + wp.x + ":" + wp.y + ":" + wp.z + ":" + wp.rotx + ":" + wp.roty);
                            }
                            SW.Dispose();
                        }
                    }
                }
            }
        }
        public bool EnoughMoney(int amount)
        {
            if (this.money >= amount)
                return true;
            return false;
        }
        public void ReviewTimer()
        {
            this.canusereview = false;
            System.Timers.Timer Clock = new System.Timers.Timer(1000 * Server.reviewcooldown);
            Clock.Elapsed += delegate { this.canusereview = true; Clock.Dispose(); };
            Clock.Start();
        }

        public void TntAtATime()
        {
            new Thread(() =>
            {
                CurrentAmountOfTnt += 1;
                switch (TntWarsGame.GetTntWarsGame(this).GameDifficulty)
                {
                    case TntWarsGame.TntWarsDifficulty.Easy:
                        Thread.Sleep(3250);
                        break;

                    case TntWarsGame.TntWarsDifficulty.Normal:
                        Thread.Sleep(2250);
                        break;

                    case TntWarsGame.TntWarsDifficulty.Hard:
                    case TntWarsGame.TntWarsDifficulty.Extreme:
                        Thread.Sleep(1250);
                        break;
                }
                CurrentAmountOfTnt -= 1;
            }).Start();
        }

        public string ReadString(int count = 64)
        {
            if (Reader == null) return null;
            var chars = new byte[count];
            Reader.Read(chars, 0, count);
            return Encoding.UTF8.GetString(chars).TrimEnd().Replace("\0", string.Empty);

        }
    }
}

