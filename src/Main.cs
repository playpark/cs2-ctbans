using CounterStrikeSharp.API.Core;

public partial class Plugin : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "CT Bans";
    public override string ModuleAuthor => "DeadSwim, continued by exkludera";
    public override string ModuleVersion => "1.0.4";

    public static Plugin Instance { get; set; } = new();

    public static readonly bool?[] banned = new bool?[64];
    public static readonly string?[] remaining = new string?[64];
    public static readonly string?[] reason = new string?[64];
    public static readonly int?[] Showinfo = new int?[64];

    // Track when players are alive
    public static readonly bool?[] isPlayerAlive = new bool?[64];
    public static readonly DateTime?[] aliveStartTime = new DateTime?[64];
    public static readonly int?[] timeServed = new int?[64];

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerConnectFull>(EventPlayerConnectFull);
        RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(EventPlayerDeath);
        RegisterEventHandler<EventRoundEnd>(EventRoundEnd);
        RegisterEventHandler<EventPlayerDisconnect>(EventPlayerDisconnect);
        RegisterListener<Listeners.OnTick>(OnTick);
        AddCommandListener("jointeam", OnPlayerChangeTeam, HookMode.Pre);

        RegisterCommands();

        Instance = this;
        Database.Load();
    }

    public override void Unload(bool hotReload)
    {
        DeregisterEventHandler<EventPlayerConnectFull>(EventPlayerConnectFull);
        DeregisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);
        DeregisterEventHandler<EventPlayerDeath>(EventPlayerDeath);
        DeregisterEventHandler<EventRoundEnd>(EventRoundEnd);
        DeregisterEventHandler<EventPlayerDisconnect>(EventPlayerDisconnect);
        RemoveListener<Listeners.OnTick>(OnTick);
        RemoveCommandListener("jointeam", OnPlayerChangeTeam, HookMode.Pre);

        UnregisterCommands();
    }

    public Config Config { get; set; } = new Config();
    public void OnConfigParsed(Config config)
    {
        Config = config;
    }
}
