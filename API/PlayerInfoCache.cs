using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCPRandomCoin.API;

internal struct PlayerInfoCache
{
    public Player Player { get; }
    public bool IsScp;
    public CoinEffects OngoingEffect;
    public float Health;
    public float MaxHealth;
    public int ItemCount;
    public Room CurrentRoom;
    public Lift? Lift;
    public bool HasLight;

    public PlayerInfoCache(Player player)
    {
        Player = player;
        Lift = player.Lift;
        CurrentRoom = player.CurrentRoom;
        ItemCount = player.Items.Count;
        Health = player.Health;
        MaxHealth = player.MaxHealth;
        OngoingEffect = EventHandlers.HasOngoingEffect.ContainsKey(player) ? EventHandlers.HasOngoingEffect[player] : CoinEffects.Nothing;
        HasLight = EventHandlers.HasALight.ContainsKey(player);
        IsScp = player.IsScp;
    }
}
