using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Pickups.Projectiles;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using SCPRandomCoin.API;
using SCPRandomCoin.CoroutineEffects;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SCPRandomCoin;

internal class EffectHandler
{
    public const int ChaosThreshold = 4;
    public const int DangerThreshold = 9;

    // ----------------------------------------------------------------------------
    // Effect-specific variables
    // ----------------------------------------------------------------------------
    public static int turnedScps = 0;
    // ----------------------------------------------------------------------------
    public static bool didCoinForAll = false;
    // ----------------------------------------------------------------------------
    public static Dictionary<Player, CoinEffects> HasOngoingEffect = new();
    // ----------------------------------------------------------------------------
    public static Dictionary<EffectGrenadeProjectile, HashSet<Player>> DiedToGrenade = new();
    // ----------------------------------------------------------------------------
    public static Player? CoinActivatedWarhead = null;
    // ----------------------------------------------------------------------------

    public static void Reset()
    {
        turnedScps = 0;
        didCoinForAll = false;
        CoinActivatedWarhead = null;

        HasOngoingEffect.Clear();
        DiedToGrenade.Clear();
    }

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
                + UnityEngine.Random.Range(-1f, 1f) * Vector3.forward * 0.1f;
            Pickup.CreateAndSpawn(ItemType.Coin, item.Position + delta, default, null);
        }
    }


    public static void OnCoinFlip(Player player, bool isTails)
    {
        if (SCPRandomCoin.Singleton == null) return;

        var translation = SCPRandomCoin.Singleton.Translation;
        var config = SCPRandomCoin.Singleton.Config;

        var doesBreak = UnityEngine.Random.Range(1, 101) < config.CoinBreakPercent;

        if (isTails)
        {
            if (doesBreak && config.CoinBreakOnTails)
            {
                coinBreak(player, player.CurrentItem);
                player.ShowHint(coinBreak(player, player.CurrentItem), 5);
            }
            return;
        }

        var effects = config.Effects.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // -------------------------------------------------------------------------------------------------------------------------
        // SPECIAL INTERACTIONS BEFORE CHOOSING EFFECT
        if (DiedToGrenade.Values.Any(x => x.Count > 0))
        {
            if (effects.ContainsKey(CoinEffects.ReSpawnSpectators))
                effects[CoinEffects.ReSpawnSpectators] = effects.Values.Max();
        }
        if (Warhead.IsInProgress && player != CoinActivatedWarhead)
        {
            doesBreak = false;
            if (effects.ContainsKey(CoinEffects.TpToRandom))
                effects[CoinEffects.TpToRandom] = effects.Values.Max();
            if (effects.ContainsKey(CoinEffects.CoinForAll))
                effects[CoinEffects.CoinForAll] = effects.Values.Max();
        }
        // -------------------------------------------------------------------------------------------------------------------------

        var infoCache = new PlayerInfoCache(player);
        CoinEffects effect = effects.GetRandomKeyByWeight(x => CanHaveEffect(x, infoCache));
        Log.Debug($"Player {player.DisplayNickname} got {effect}");

        var hint = _doEffect(effect, player, out doesBreak);

        if (doesBreak || player.Role.Team == Team.SCPs)
        {
            hint += "\n" + coinBreak(player, player.CurrentItem);
        }
        ShowCoinEffectHint(player, hint);
    }

    public static void ForceCoinEffect(Player player, CoinEffects effect)
    {
        var hint = _doEffect(effect, player, out var doesBreak);
        ShowCoinEffectHint(player, hint);
    }

    private static void ShowCoinEffectHint(Player player, string hint)
    {
        if (!string.IsNullOrWhiteSpace(hint))
        {
            player.ShowHint(hint, 5 * (hint.Where(x => x == '\n').Count() + 1));
        }
    }

    private static bool CanHaveEffect(CoinEffects str, PlayerInfoCache player)
    {
        bool notScp = !player.IsScp;
        var dangerThreshold = Round.ElapsedTime.TotalMinutes >= DangerThreshold;
        var chaosThreshold = Round.ElapsedTime.TotalMinutes >= ChaosThreshold;
        var ongoing = player.OngoingEffect;

        var isOngoingEffect = ongoing != CoinEffects.Nothing;
        var canTp = ongoing != CoinEffects.Jail;

        var gonnaSwap = GoingToSwapCoroutine.GoingToSwap.Contains(player.Player);
        var readySwap = GoingToSwapCoroutine.ReadyToSwap.Contains(player.Player);

        return str switch
        {
            // effects that can happen in the lobby
            CoinEffects.Heal => player.Health < player.MaxHealth,
            CoinEffects.OneHp => notScp && player.Health > 1,
            CoinEffects.GetCandy => notScp,
            CoinEffects.GetItem => notScp,
            CoinEffects.Shrink => notScp,
            CoinEffects.GetALight => !player.HasLight,
            CoinEffects.SpawnGrenade => player.CurrentRoom.Type != RoomType.Lcz914 && player.Lift == null, // just in case they're trapped in the machine

            // effects that can only happen once the game has started
            CoinEffects.LookLikeScp => Round.IsStarted && !isOngoingEffect && notScp,
            CoinEffects.ReSpawnSpectators => Round.IsStarted && Player.Get(RoleTypeId.Spectator).Any(),
            CoinEffects.FakeScpDeath => Round.IsStarted && Player.Get(Team.SCPs).Any(x => x.Role.Type != RoleTypeId.Scp0492),
            CoinEffects.Jail => Round.IsStarted && !isOngoingEffect && player.Lift == null,

            // slightly chaotic effects
            CoinEffects.TpToRandom => !Warhead.IsDetonated && canTp && chaosThreshold,
            CoinEffects.CoinForAll => !didCoinForAll && chaosThreshold,
            CoinEffects.TpToScp => canTp && notScp && Player.Get(TpToScpSelector).Any() && chaosThreshold,
            CoinEffects.Snapback => !isOngoingEffect && chaosThreshold,
            CoinEffects.BecomeSwappable => !readySwap && !gonnaSwap && chaosThreshold,
            CoinEffects.BecomeScp => !isOngoingEffect && notScp && chaosThreshold && turnedScps < 2,
            CoinEffects.OneInTheChamber => !isOngoingEffect && notScp && chaosThreshold,

            // very chaotic and dangerous effects
            CoinEffects.StartWarhead => !Warhead.IsInProgress && !Warhead.IsDetonated && dangerThreshold,
            CoinEffects.PocketDimension => dangerThreshold,

            // dependent on other effects
            CoinEffects.StopWarhead => CoinActivatedWarhead != null && Warhead.IsInProgress,
            CoinEffects.RemoveSwappable => readySwap,
            CoinEffects.DoSwap => !readySwap && !gonnaSwap && GoingToSwapCoroutine.ReadyToSwap.Any(x => x != player.Player),
            _ => true,
        };
    }

    private static bool TpToScpSelector(Player player)
        => player.IsScp && player.Role != RoleTypeId.Scp079;

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

        { EffectType.Invigorated, 60 },
        { EffectType.Invisible, 999 },
        { EffectType.Scp207, 60 },
        { EffectType.Vitality, 60 },
    };


    private static string coinBreak(Player player, Item coin)
    {
        if (player.HasItem(coin))
        {
            player.RemoveItem(coin);
            return SCPRandomCoin.Singleton?.Translation.CoinBreak ?? "";
        }
        return "";
    }

    private static string _doEffect(CoinEffects effect, Player player, out bool doesBreak)
    {
        doesBreak = false;
        if (SCPRandomCoin.Singleton == null) return "";

        var translation = SCPRandomCoin.Singleton.Translation;
        var config = SCPRandomCoin.Singleton.Config;

        var hintLines = new List<string>();

        var formatInfo = new Dictionary<string, object>
        {
            { "name", player.DisplayNickname },
        };

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
                    player.Health = 1;
                    hintLines.Add(translation.OneHp);
                    break;
                }
            case CoinEffects.Heal:
                {
                    player.Heal(player.MaxHealth);
                    player.DisableAllEffects();
                    hintLines.Add(translation.Heal);
                    break;
                }
            case CoinEffects.TpToRandom:
                {
                    var role = new[] { RoleTypeId.Scientist, RoleTypeId.FacilityGuard, RoleTypeId.Scp049, RoleTypeId.Scp173, RoleTypeId.Scp939, RoleTypeId.Scp096 }.GetRandomValue();
                    player.Position = role.GetRandomSpawnLocation().Position;
                    hintLines.Add(translation.Tp);
                    break;
                }
            case CoinEffects.TpToScp:
                {
                    var scp = Player.Get(TpToScpSelector).GetRandomValue();
                    player.Position = scp.Position;
                    hintLines.Add(translation.Tp);
                    break;
                }
            case CoinEffects.BecomeScp:
                {
                    var roles = new List<RoleTypeId> { RoleTypeId.Scp049, RoleTypeId.Scp096, RoleTypeId.Scp3114, RoleTypeId.Scp106, RoleTypeId.Scp939, RoleTypeId.Scp173 };
                    if (Player.Get(Team.SCPs).Count() > 0)
                        roles.Add(RoleTypeId.Scp079);

                    var scp = roles.GetRandomValue();
                    player.Role.Set(scp, RoleSpawnFlags.AssignInventory);
                    turnedScps++;
                    break;
                }
            case CoinEffects.LoseItem:
                {
                    var item = player.Items.Where(item => item != player.CurrentItem).GetRandomValue();
                    player.RemoveItem(item);
                    formatInfo["item"] = item.Type;
                    hintLines.Add(translation.LoseItem);
                    break;
                }
            case CoinEffects.GetItem:
                {
                    var item = config.ItemList.GetRandomKeyByWeight();
                    var pickup = Pickup.CreateAndSpawn(item, player.Position, default, player);
                    formatInfo["item"] = item;
                    hintLines.Add(translation.GetItem);
                    break;
                }
            case CoinEffects.LookLikeScp:
                {
                    Timing.RunCoroutine(LookLikeScpCoroutine.Coroutine(player, 60));
                    hintLines.Add(translation.FeelFunny);
                    break;
                }
            case CoinEffects.BecomeWide:
                {
                    player.Scale = new(1.1f, 1f, 1.1f);
                    hintLines.Add(translation.FeelFunny);
                    break;
                }
            case CoinEffects.ReSpawnSpectators:
                {
                    var spectators = Player.Get(RoleTypeId.Spectator).Take(5).ToList();
                    formatInfo["count"] = spectators.Count;
                    foreach (Player spectator in spectators)
                    {
                        spectator.Role.Set(player.Role.Type);
                        spectator.Position = player.Position;
                        spectator.ShowHint(translation.Respawned.Format(formatInfo), 25);
                    }
                    hintLines.Add(translation.Respawn);

                    DiedToGrenade.Clear();
                    break;
                }
            case CoinEffects.GetCandy:
                {
                    var item = CandyTypes.GetRandomValue();
                    var pickup = (Exiled.API.Features.Pickups.Scp330Pickup)Pickup.CreateAndSpawn(ItemType.SCP330, player.Position, default, player);
                    pickup.Candies.Add(item);
                    formatInfo["item"] = $"{item} Candy";
                    hintLines.Add(translation.GetItem);
                    break;
                }
            case CoinEffects.RandomEffect:
                {
                    var ef = Effects.GetRandomValue();
                    player.EnableEffect(ef.Key, 10, ef.Value, false);
                    formatInfo["item"] = ef.Key;
                    hintLines.Add(translation.GetItem);
                    break;
                }
            case CoinEffects.SpawnGrenade:
                {
                    var grenade = (ExplosiveGrenade)Item.Create(ItemType.GrenadeHE);
                    grenade.FuseTime = 4;
                    var grenadeSpawn = grenade.SpawnActive(player.Position);
                    if (grenadeSpawn != null)
                        DiedToGrenade[grenadeSpawn] = new();
                    hintLines.Add(translation.Grenade);
                    break;
                }
            case CoinEffects.CoinForAll:
                {
                    didCoinForAll = true;
                    formatInfo["item"] = "Coin";
                    foreach (Player alivePlayer in Player.Get(x => x.IsAlive))
                    {
                        if (alivePlayer.IsScp)
                        {
                            alivePlayer.CurrentItem = alivePlayer.AddItem(ItemType.Coin);
                            alivePlayer.ShowHint(translation.GetItem.Format(formatInfo), 10);
                        }
                        else
                        {
                            var pickup = Pickup.CreateAndSpawn(ItemType.Coin, alivePlayer.Position, default, alivePlayer);
                            if (alivePlayer != player)
                            {
                                alivePlayer.ShowHint(translation.GetItem.Format(formatInfo), 10);
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
                    player.Scale = new(1.1f, 0.5f, 1.1f);
                    hintLines.Add(translation.FeelFunny);
                    break;
                }
            case CoinEffects.StartWarhead:
                {
                    Warhead.Status = WarheadStatus.InProgress;
                    hintLines.Add(translation.Warhead);
                    CoinActivatedWarhead = player;
                    break;
                }
            case CoinEffects.StopWarhead:
                {
                    Warhead.Status = WarheadStatus.NotArmed;
                    break;
                }
            case CoinEffects.FakeScpDeath:
                {
                    var scp = Player.Get(x => x.IsScp && x.Role.Type != RoleTypeId.Scp0492).GetRandomValue();
                    NineTailedFoxAnnouncer.ConvertSCP(scp.Role.Type, out string withoutSpace, out string withSpace);
                    var team = player.IsScp ? "by Automatic Security System" : NineTailedFoxAnnouncer.ConvertTeam(player.Role.Team, player.UnitName);
                    team = player.Role.Team == Team.FoundationForces ? ". " + team : " " + team;
                    var announcement = $"contained successfully{team}";
                    Cassie.MessageTranslated($"SCP {withSpace} {announcement}", $"SCP-{withoutSpace} {announcement}".ToUpper());
                    break;
                }
            case CoinEffects.Jail:
                {
                    Timing.RunCoroutine(JailCoroutine.Coroutine(player, 15));
                    break;
                }
            case CoinEffects.GetALight:
                {
                    Timing.RunCoroutine(GetALightCoroutine.Coroutine(player, 30));
                    break;
                }
            case CoinEffects.Snapback:
                {
                    Timing.RunCoroutine(SnapbackCoroutine.Coroutine(player, 15));
                    break;
                }
            case CoinEffects.BecomeSwappable:
                {
                    GoingToSwapCoroutine.ReadyToSwap.Add(player);
                    hintLines.Add(translation.DestabilizedSwap);
                    break;
                }
            case CoinEffects.RemoveSwappable:
                {
                    GoingToSwapCoroutine.ReadyToSwap.Remove(player);
                    hintLines.Add(translation.StabilizedSwap);
                    break;
                }
            case CoinEffects.DoSwap:
                {
                    Timing.RunCoroutine(GoingToSwapCoroutine.Coroutine(player, 5));
                    break;
                }
            case CoinEffects.OneInTheChamber:
                {
                    Timing.RunCoroutine(OneInTheChamberCoroutine.Coroutine(player));
                    break;
                }
            case CoinEffects.PocketDimension:
                {
                    player.EnableEffect(EffectType.PocketCorroding, 1, 1);
                    break;
                }
        }

        return string.Join("\n", hintLines).Format(formatInfo);
    }
}
