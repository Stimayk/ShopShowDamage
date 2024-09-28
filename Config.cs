using CounterStrikeSharp.API.Core;

namespace ShopShowDamage
{
    public class ShopShowDamageConfig : BasePluginConfig
    {
        public float NotifyDuration { get; set; } = 3.5f;

        public string? ItemName { get; set; } = "ShowDamage";
        public string? Name { get; set; } = "Отображение урона";
        public int Price { get; set; } = 1000;
        public int SellPrice { get; set; } = 500;
        public int Duration { get; set; } = 86400;
    }
}