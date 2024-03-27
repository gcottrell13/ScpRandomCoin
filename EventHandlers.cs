using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using SCPRandomCoin.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SCPRandomCoin;

internal static class EventHandlers
{
    public static IEnumerator<float> OnCoinFlip(FlippingCoinEventArgs ev)
    {
        if (SCPRandomCoin.Singleton == null) yield break;
        if (!Round.InProgress) yield break;

        var translation = SCPRandomCoin.Singleton.Translation;
        var config = SCPRandomCoin.Singleton.Config;

        var hintLines = new List<string>();

        void coinBreak(Player player, Item coin)
        {
            if (player.HasItem(coin))
            {
                player.RemoveItem(coin);
                hintLines.Add(translation.CoinBreak);
            }
        }

        void showHint()
        {
            if (hintLines.Any())
            {
                ev.Player.ShowHint(string.Join("\n", hintLines), hintLines.Count * 5);
                hintLines.Clear();
            }
        }

        var doesBreak = UnityEngine.Random.Range(1, 101) < config.CoinBreakPercent;

        string effectString = config.Effects.GetRandomKeyByWeight() ?? "";
        if (!Enum.TryParse<CoinEffects>(effectString, out var effect))
        {
            effect = CoinEffects.GetPoorItem;
            Log.Warn($"Unknown effect {effectString}, defaulting to {effect}");
        }

        if (ev.IsTails)
        {
            if (doesBreak && config.CoinBreakOnTails)
            {
                coinBreak(ev.Player, ev.Player.CurrentItem);
                showHint();
            }
            yield break;
        }

        yield return Timing.WaitForSeconds(2);

        Log.Info($"Player {ev.Player.DisplayNickname} got {effectString}");

        switch (effect)
        {
            case CoinEffects.Nothing:
                {
                    if (!config.CoinBreakOnTails)
                    {
                        doesBreak = false;
                    }
                    hintLines.Add(translation.Nothing);
                    break;
                }
            case CoinEffects.OneHp:
                {
                    ev.Player.Health = 1;
                    hintLines.Add(translation.OneHp);
                    break;
                }
            case CoinEffects.Heal:
                {
                    ev.Player.Heal(100);
                    ev.Player.DisableAllEffects();
                    ev.Player.Scale = new(1, 1, 1);
                    hintLines.Add(translation.Heal);
                    break;
                }
            case CoinEffects.TpToRandom:
                {
                    var role = new[] { RoleTypeId.Scientist, RoleTypeId.FacilityGuard, RoleTypeId.Scp049, RoleTypeId.Scp173, RoleTypeId.Scp939 }.GetRandomValue();
                    ev.Player.Position = role.GetRandomSpawnLocation().Position;
                    hintLines.Add(translation.Tp);
                    break;
                }
            case CoinEffects.TpToScp:
                {
                    var scp = Player.Get(Team.SCPs).Where(x => x.Role != RoleTypeId.Scp079).GetRandomValue();
                    ev.Player.Position = scp.Position;
                    hintLines.Add(translation.Tp);
                    break;
                }
            case CoinEffects.BecomeScp:
                {
                    var scp = new[] { RoleTypeId.Scp049, RoleTypeId.Scp096, RoleTypeId.Scp3114, RoleTypeId.Scp106, RoleTypeId.Scp939, RoleTypeId.Scp173 }.GetRandomValue();
                    ev.Player.Role.Set(scp, RoleSpawnFlags.AssignInventory);
                    break;
                }
            case CoinEffects.LoseItem:
                {
                    var item = ev.Player.Items.GetRandomValue();
                    if (item == ev.Item)
                    {
                        doesBreak = true;
                    }
                    else
                    {
                        ev.Player.RemoveItem(item);
                        hintLines.Add(translation.LoseItem.Replace("{item}", item.Type.ToString()));
                    }
                    break;
                }
            case CoinEffects.GetGoodItem:
                {
                    var item = GoodItems.GetRandomValue();
                    Pickup.CreateAndSpawn(item, ev.Player.Position, default, ev.Player);
                    hintLines.Add(translation.GetItem.Replace("{item}", item.ToString()));
                    break;
                }
            case CoinEffects.GetPoorItem:
                {
                    var item = PoorItems.GetRandomValue();
                    Pickup.CreateAndSpawn(item, ev.Player.Position, default, ev.Player);
                    hintLines.Add(translation.GetItem.Replace("{item}", item.ToString()));
                    break;
                }
            case CoinEffects.LookLikeScp:
                {
                    var scp = new[] { RoleTypeId.Scp049, RoleTypeId.Scp096, RoleTypeId.Scp3114, RoleTypeId.Scp106, RoleTypeId.Scp939, RoleTypeId.Scp173 }.GetRandomValue();
                    ev.Player.ChangeAppearance(scp);
                    hintLines.Add(translation.LookLikeScp);
                    Timing.CallDelayed(15, () =>
                    {
                        ev.Player.ChangeAppearance(ev.Player.Role);
                    });
                    break;
                }
            case CoinEffects.BecomeWide:
                {
                    ev.Player.Scale = new(1.3f, 0.7f, 1.3f);
                    hintLines.Add(translation.Wide);
                    break;
                }
            case CoinEffects.ReSpawnSpectators:
                {
                    var spectators = Player.Get(RoleTypeId.Spectator).Take(5).ToList();
                    foreach (Player spectator in spectators)
                    {
                        spectator.Role.Set(ev.Player.Role.Type);
                        spectator.Position = ev.Player.Position;
                        spectator.ShowHint(translation.Respawned.Replace("{name}", ev.Player.DisplayNickname), 25);
                    }
                    hintLines.Add(translation.Respawn.Replace("{count}", spectators.Count.ToString()));
                    break;
                }
            case CoinEffects.GetCandy:
                {
                    var item = CandyTypes.GetRandomValue();
                    var pickup = (Exiled.API.Features.Pickups.Scp330Pickup) Pickup.CreateAndSpawn(ItemType.SCP330, ev.Player.Position, default, ev.Player);
                    pickup.Candies.Add(item);
                    hintLines.Add(translation.GetItem.Replace("{item}", item.ToString() + " Candy"));
                    break;
                }
            case CoinEffects.RandomEffect:
                {
                    var ef = Effects.GetRandomValue();
                    ev.Player.EnableEffect(ef.Key, 10, ef.Value, false);
                    hintLines.Add(translation.GetItem.Replace("{item}", ef.Key.ToString()));
                    break;
                }
            case CoinEffects.SpawnGrenade:
                {
                    var grenade = (ExplosiveGrenade) Item.Create(ItemType.GrenadeHE);
                    grenade.FuseTime = 3;
                    grenade.SpawnActive(ev.Player.Position);
                    hintLines.Add(translation.Grenade);
                    break;
                }
        }

        if (doesBreak)
        {
            coinBreak(ev.Player, ev.Player.CurrentItem);
        }
        showHint();
    }



    public static readonly List<ItemType> GoodItems = new()
    {
        ItemType.Jailbird,
        ItemType.KeycardO5,
        ItemType.ParticleDisruptor,
        ItemType.SCP500,
        ItemType.SCP207,
    };

    public static readonly List<ItemType> PoorItems = new()
    {
        ItemType.KeycardJanitor,
        ItemType.ArmorLight,
        ItemType.Flashlight,
        ItemType.Lantern,
        ItemType.Painkillers,

    };

    public static readonly List<CandyKindID> CandyTypes = new()
    {
        CandyKindID.Rainbow,
        CandyKindID.Green,
        CandyKindID.Green,
        CandyKindID.Green,
        CandyKindID.Green,
        CandyKindID.Blue,
        CandyKindID.Blue,
        CandyKindID.Blue,
        CandyKindID.Red,
        CandyKindID.Red,
        CandyKindID.Red,
        CandyKindID.Purple,
        CandyKindID.Purple,
        CandyKindID.Yellow,
        CandyKindID.Yellow,
        CandyKindID.Pink,
    };

    public static readonly Dictionary<EffectType, float> Effects = new()
    {
        { EffectType.Blinded, 60 },
        { EffectType.Flashed, 5 },
        { EffectType.SinkHole, 10 },
        { EffectType.Stained, 10 },
        { EffectType.PocketCorroding, 1 },

        { EffectType.Invigorated, 60 },
        { EffectType.Invisible, 999 },
        { EffectType.Scp207, 60 },
        { EffectType.Vitality, 60 },
    };
}
