using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using ShopAPI;
using System.Collections.Concurrent;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace ShopShowDamage
{
    public class ShopShowDamage : BasePlugin, IPluginConfig<ShopShowDamageConfig>
    {
        public override string ModuleName => "[SHOP] Show Damage";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.1.0";

        private IShopApi? SHOP_API;
        private const string CategoryName = "ShowDamage";
        private readonly PlayerShowDamage[] playerShowDamages = new PlayerShowDamage[65];
        private readonly ConcurrentDictionary<CCSPlayerController, string> messages = new();
        private readonly ConcurrentDictionary<CCSPlayerController, Timer> deleteTimers = new();

        public ShopShowDamageConfig Config { get; set; } = new();

        public void OnConfigParsed(ShopShowDamageConfig config)
        {
            Config = config;
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RegisterListener<Listeners.OnTick>(ShowDamageMessages);

            InitializeShopItems();
        }

        public override void Unload(bool hotReload)
        {
            foreach (var timer in deleteTimers.Values)
            {
                timer.Kill();
            }
            deleteTimers.Clear();
            messages.Clear();

            DeregisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RemoveListener<Listeners.OnTick>(ShowDamageMessages);
            UnloadingShopItems();
        }

        private void InitializeShopItems()
        {
            if (Config.ItemName == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Отображение урона");

            Task.Run(async () =>
            {
                int itemId = await SHOP_API.AddItem(
                    Config.ItemName,
                    Config.Name!,
                    CategoryName,
                    Config.Price!,
                    Config.SellPrice!,
                    Config.Duration!
                );
                SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
            }).Wait();
        }

        private void UnloadingShopItems()
        {
            if (Config.ItemName == null || SHOP_API == null) return;

            SHOP_API.UnregisterCategory(CategoryName, true);
        }

        [GameEventHandler()]
        public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            var attacker = @event.Attacker;
            var userid = @event.Userid;

            if (attacker == null || !attacker.IsValid || attacker.IsBot || attacker == userid ||
            (attacker.TeamNum == (userid?.TeamNum ?? 0)) || (playerShowDamages[attacker.Slot] == null))
            {
                return HookResult.Continue;
            }

            var dmgHealth = @event.DmgHealth;
            var health = @event.Health;
            var hudMessage = Localizer["HUD", dmgHealth, userid?.PlayerName ?? "Unknown", health];

            ManageTimerAndMessage(attacker, hudMessage);

            return HookResult.Continue;
        }

        private void ManageTimerAndMessage(CCSPlayerController attacker, string hudMessage)
        {
            if (deleteTimers.TryRemove(attacker, out var timer))
            {
                timer.Kill();
            }

            messages[attacker] = hudMessage;
            var newTimer = new Timer(Config.NotifyDuration, () =>
            {
                if (messages.TryRemove(attacker, out _) && deleteTimers.TryRemove(attacker, out var removeTimer))
                {
                    removeTimer?.Kill();
                }
            });

            deleteTimers[attacker] = newTimer;
        }

        private void ShowDamageMessages()
        {
            foreach (var entry in messages)
            {
                PrintHtml(entry.Key, entry.Value);
            }
        }

        private static void PrintHtml(CCSPlayerController player, string hudContent)
        {
            var eventShowSurvivalRespawnStatus = new EventShowSurvivalRespawnStatus(false)
            {
                LocToken = hudContent,
                Duration = 5L,
                Userid = player
            };
            eventShowSurvivalRespawnStatus.FireEvent(false);
        }

        public HookResult OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName, int buyPrice, int sellPrice, int duration, int count)
        {
            playerShowDamages[player.Slot] = new PlayerShowDamage(itemId);
            return HookResult.Continue;
        }

        public HookResult OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1)
            {
                playerShowDamages[player.Slot] = new PlayerShowDamage(itemId);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
            return HookResult.Continue;
        }

        public HookResult OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerShowDamages[player.Slot] = null!;
            return HookResult.Continue;
        }

        public record class PlayerShowDamage(int ItemID);
    }
}