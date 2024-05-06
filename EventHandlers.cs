using CommandSystem.Commands.RemoteAdmin;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using PluginAPI.Events;
using SCPRandomCoin.API;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SCPRandomCoin;

internal static class EventHandlers
{
    static int turnedScps = 0;
    static bool didCoinForAll = false;

    static Dictionary<Player, CoinEffects> hasOngoingEffect = new();

    public static void OnRoundStarted()
    {
        turnedScps = 0;
        didCoinForAll = false;
        hasOngoingEffect = new();

        SpawnExtraCoins();
    }

    public static IEnumerator<float> OnCoinFlip(FlippingCoinEventArgs ev)
    {
        if (SCPRandomCoin.Singleton == null) yield break;

        var translation = SCPRandomCoin.Singleton.Translation;
        var config = SCPRandomCoin.Singleton.Config;

        var hintLines = new List<string>();

        var formatInfo = new Dictionary<string, object>
        {
            { "name", ev.Player.DisplayNickname },
        };

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
                ev.Player.ShowHint(string.Join("\n", hintLines).Format(formatInfo), hintLines.Count * 5);
                hintLines.Clear();
            }
        }

        var doesBreak = UnityEngine.Random.Range(1, 101) < config.CoinBreakPercent;

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

        CoinEffects effect = config.Effects.GetRandomKeyByWeight(x => CanHaveEffect(x, ev.Player));
        Log.Info($"Player {ev.Player.DisplayNickname} got {effect}");

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
                    ev.Player.Heal(ev.Player.MaxHealth);
                    ev.Player.DisableAllEffects();
                    ev.Player.Scale = Vector3.one;
                    hintLines.Add(translation.Heal);
                    break;
                }
            case CoinEffects.TpToRandom:
                {
                    var role = new[] { RoleTypeId.Scientist, RoleTypeId.FacilityGuard, RoleTypeId.Scp049, RoleTypeId.Scp173, RoleTypeId.Scp939, RoleTypeId.Scp096 }.GetRandomValue();
                    ev.Player.Position = role.GetRandomSpawnLocation().Position;
                    hintLines.Add(translation.Tp);
                    break;
                }
            case CoinEffects.TpToScp:
                {
                    var scp = Player.Get(TpToScpSelector).GetRandomValue();
                    ev.Player.Position = scp.Position;
                    hintLines.Add(translation.Tp);
                    break;
                }
            case CoinEffects.BecomeScp:
                {
                    var roles = new List<RoleTypeId> { RoleTypeId.Scp049, RoleTypeId.Scp096, RoleTypeId.Scp3114, RoleTypeId.Scp106, RoleTypeId.Scp939, RoleTypeId.Scp173 };
                    if (Player.Get(Team.SCPs).Count() > 0)
                        roles.Add(RoleTypeId.Scp079);

                    ev.Player.Scale = Vector3.one;
                    var scp = roles.GetRandomValue();
                    ev.Player.Role.Set(scp, RoleSpawnFlags.AssignInventory);
                    turnedScps++;
                    break;
                }
            case CoinEffects.LoseItem:
                {
                    var item = ev.Player.Items.Where(item => item != ev.Item).GetRandomValue();
                    ev.Player.RemoveItem(item);
                    formatInfo["item"] = item.Type;
                    hintLines.Add(translation.LoseItem);
                    break;
                }
            case CoinEffects.GetItem:
                {
                    var item = config.ItemList.GetRandomKeyByWeight();
                    var pickup = Pickup.CreateAndSpawn(item, ev.Player.Position, default, ev.Player);
                    formatInfo["item"] = item;
                    hintLines.Add(translation.GetItem);
                    break;
                }
            case CoinEffects.LookLikeScp:
                {
                    var scp = new[] { RoleTypeId.Scp049, RoleTypeId.Scp096, RoleTypeId.Scp3114, RoleTypeId.Scp106, RoleTypeId.Scp939, RoleTypeId.Scp173 }.GetRandomValue();
                    ev.Player.ChangeAppearance(scp);
                    hintLines.Add(translation.FeelFunny);
                    hasOngoingEffect[ev.Player] = CoinEffects.LookLikeScp;
                    Timing.CallDelayed(15, () =>
                    {
                        hasOngoingEffect.Remove(ev.Player);
                        ev.Player.ChangeAppearance(ev.Player.Role);
                    });
                    break;
                }
            case CoinEffects.BecomeWide:
                {
                    ev.Player.Scale = new(1.1f, 1f, 1.1f);
                    hintLines.Add(translation.FeelFunny);
                    break;
                }
            case CoinEffects.ReSpawnSpectators:
                {
                    var spectators = Player.Get(RoleTypeId.Spectator).Take(5).ToList();
                    formatInfo["count"] = spectators.Count;
                    foreach (Player spectator in spectators)
                    {
                        spectator.Role.Set(ev.Player.Role.Type);
                        spectator.Position = ev.Player.Position;
                        spectator.ShowHint(translation.Respawned.Format(formatInfo), 25);
                    }
                    hintLines.Add(translation.Respawn);
                    break;
                }
            case CoinEffects.GetCandy:
                {
                    var item = CandyTypes.GetRandomValue();
                    var pickup = (Exiled.API.Features.Pickups.Scp330Pickup) Pickup.CreateAndSpawn(ItemType.SCP330, ev.Player.Position, default, ev.Player);
                    pickup.Candies.Add(item);
                    formatInfo["item"] = $"{item} Candy";
                    hintLines.Add(translation.GetItem);
                    break;
                }
            case CoinEffects.RandomEffect:
                {
                    var ef = Effects.GetRandomValue();
                    ev.Player.EnableEffect(ef.Key, 10, ef.Value, false);
                    formatInfo["item"] = ef.Key;
                    hintLines.Add(translation.GetItem);
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
            case CoinEffects.CoinForAll:
                {
                    didCoinForAll = true;
                    formatInfo["item"] = "Coin";
                    foreach (Player player in Player.Get(x => x.IsAlive))
                    {
                        if (player.IsScp)
                        {
                            player.CurrentItem = player.AddItem(ItemType.Coin);
                            player.ShowHint(translation.GetItem.Format(formatInfo), 10);
                        }
                        else
                        {
                            var pickup = Pickup.CreateAndSpawn(ItemType.Coin, player.Position, default, player);
                            if (player != ev.Player)
                            {
                                player.ShowHint(translation.GetItem.Format(formatInfo), 10);
                            }
                            else
                            {
                                hintLines.Add(translation.CoinForAll);
                            }
                        }
                    }
                    break;
                }
            case CoinEffects.Shrink:
                {
                    ev.Player.Scale = new(1.1f, 0.5f, 1.1f);
                    hintLines.Add(translation.FeelFunny);
                    break;
                }
            case CoinEffects.StartWarhead:
                {
                    Warhead.Start();
                    hintLines.Add(translation.Warhead);
                    break;
                }
        }

        if (doesBreak || ev.Player.Role.Team == Team.SCPs)
        {
            coinBreak(ev.Player, ev.Player.CurrentItem);
        }
        showHint();
    }

    private static bool CanHaveEffect(CoinEffects str, Player player)
    {
        bool notScp = player.Role.Team != Team.SCPs;
        var fiveMinutes = Round.ElapsedTime.TotalMinutes >= 5;

        return str switch
        {
            CoinEffects.Heal => player.Health < player.MaxHealth,
            CoinEffects.OneHp => notScp && player.Health > 1,
            CoinEffects.TpToScp => notScp && Player.Get(TpToScpSelector).Any() && fiveMinutes,
            CoinEffects.ReSpawnSpectators => Player.Get(RoleTypeId.Spectator).Any(),
            CoinEffects.LoseItem => notScp && player.Items.Count > 1,
            CoinEffects.LookLikeScp => notScp && !hasOngoingEffect.ContainsKey(player),
            CoinEffects.BecomeScp => notScp && fiveMinutes && turnedScps < 2,
            CoinEffects.GetCandy => notScp,
            CoinEffects.GetItem => notScp,
            CoinEffects.Shrink => notScp,
            CoinEffects.CoinForAll => !didCoinForAll,
            CoinEffects.SpawnGrenade => player.CurrentRoom.Type != RoomType.Lcz914, // just in case they're trapped in the machine
            CoinEffects.StartWarhead => !Warhead.IsInProgress && fiveMinutes,
            _ => true,
        };
    }

    private static bool TpToScpSelector(Player player) 
        => player.Role.Team == Team.SCPs && player.Role != RoleTypeId.Scp079;

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

    public static void SpawnExtraCoins()
    {
        if (SCPRandomCoin.Singleton == null) 
            return;

        var config = SCPRandomCoin.Singleton.Config;
        if (config.SpawnExtraCoins <= 0)
            return;

        var scpItems = Pickup.List.Where(x => x.Type.IsScp()).Take(config.SpawnExtraCoins).ToList();
        foreach (var item in scpItems)
        {
            var delta = UnityEngine.Random.Range(-1f, 1f) * Vector3.left * 0.1f 
                + UnityEngine.Random.Range(-1f, 1f) * Vector3.forward * 1.0f;
            Pickup.CreateAndSpawn(ItemType.Coin, item.Position + delta, default, null);
        }
    }
}
