using CounterStrikeSharp.API.Core;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopShowDamage
{
    public class ShopShowDamage : BasePlugin
    {
        public override string ModuleName => "[SHOP] Show Damage";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.0";

        private IShopApi? SHOP_API;
        private const string CategoryName = "ShowDamage";
        public static JObject? JsonShowDamage { get; private set; }
        private readonly PlayerShowDamage[] playerShowDamages = new PlayerShowDamage[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/ShowDamage.json");
            if (File.Exists(configPath))
            {
                JsonShowDamage = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonShowDamage == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Отображение урона");

            foreach (var item in JsonShowDamage.Properties().Where(p => p.Value is JObject))
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(
                        item.Name,
                        (string)item.Value["name"]!,
                        CategoryName,
                        (int)item.Value["price"]!,
                        (int)item.Value["sellprice"]!,
                        (int)item.Value["duration"]!
                    );
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerShowDamages[playerSlot] = null!);

            RegisterEventHandler<EventPlayerHurt>((@event, info) =>
            {
                var attacker = @event.Attacker;
                if (attacker != null)
                {
                    if (!attacker.IsValid) return HookResult.Continue;
                    if (attacker.PlayerName == @event.Userid?.PlayerName) return HookResult.Continue;

                    if (playerShowDamages[attacker.Slot] != null)
                    {
                        attacker.PrintToCenterHtml($" Нанесён урон: <font color='red'>{@event.DmgHealth}HP</font>");
                    }
                }
                return HookResult.Continue;
            });
        }

        public void OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName,
            int buyPrice, int sellPrice, int duration, int count)
        {
            playerShowDamages[player.Slot] = new PlayerShowDamage(itemId);
        }

        public void OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1)
            {
                playerShowDamages[player.Slot] = new PlayerShowDamage(itemId);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
        }

        public void OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerShowDamages[player.Slot] = null!;
        }

        public record class PlayerShowDamage(int ItemID);
    }
}