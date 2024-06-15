
namespace SCPRandomCoin;

using System;
using Exiled.API.Features;
using Configs;
using PlayerEvent = Exiled.Events.Handlers.Player;
using ServerEvent = Exiled.Events.Handlers.Server;
using MapEvent = Exiled.Events.Handlers.Map;
using WarheadEvent = Exiled.Events.Handlers.Warhead;


internal class SCPRandomCoin : Plugin<Config, Translation>
{
    public static SCPRandomCoin? Singleton;

    public override void OnEnabled()
    {
        Singleton = this;
        PlayerEvent.FlippingCoin += EventHandlers.OnCoinFlip;
        PlayerEvent.ChangedItem += EventHandlers.OnChangedItem;
        ServerEvent.RoundStarted += EventHandlers.OnRoundStarted;
        MapEvent.ExplodingGrenade += EventHandlers.OnGrenadeExplosion;
        WarheadEvent.Stopping += EventHandlers.OnStoppingWarhead;
        base.OnEnabled();
    }

    public override void OnDisabled()
    {
        Singleton = null;
        PlayerEvent.FlippingCoin -= EventHandlers.OnCoinFlip;
        PlayerEvent.ChangedItem -= EventHandlers.OnChangedItem;
        ServerEvent.RoundStarted -= EventHandlers.OnRoundStarted;
        MapEvent.ExplodingGrenade -= EventHandlers.OnGrenadeExplosion;
        WarheadEvent.Stopping -= EventHandlers.OnStoppingWarhead;
        base.OnDisabled();
    }


    public override string Name => "RandomCoin";
    public override string Author => "GCOTTRE";
    public override Version Version => new Version(1, 0, 0);
    public override Version RequiredExiledVersion => new Version(8, 9, 5);
}
