using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Pickups.Projectiles;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Warhead;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using SCPRandomCoin.API;
using SCPRandomCoin.Commands;
using SCPRandomCoin.CoroutineEffects;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using LightToy = Exiled.API.Features.Toys.Light;

namespace SCPRandomCoin;

internal static class EventHandlers
{
    public const int ChaosThreshold = 4;
    public const int DangerThreshold = 9;

    // ----------------------------------------------------------------------------
    // Effect-specific variables
    // ----------------------------------------------------------------------------
    static int turnedScps = 0;
    // ----------------------------------------------------------------------------
    static bool didCoinForAll = false;
    // ----------------------------------------------------------------------------
    public static Dictionary<Player, CoinEffects> HasOngoingEffect = new();
    // ----------------------------------------------------------------------------
    public static Dictionary<EffectGrenadeProjectile, HashSet<Player>> DiedToGrenade = new();
    // ----------------------------------------------------------------------------
    public static Player? CoinActivatedWarhead = null;
    // ----------------------------------------------------------------------------
    public static Dictionary<Player, LightToy> HasALight = new();
    // ----------------------------------------------------------------------------
    public static HashSet<Player> ReadyToSwap = new();
    public static HashSet<Player> GoingToSwap = new();
    // ----------------------------------------------------------------------------

    public static IEnumerator<float> OnChangedItem(ChangedItemEventArgs ev)
    {
        yield return Timing.WaitForSeconds(0.5f);
        while (ev.Player.CurrentItem.Type == ItemType.Coin)
        {
            if (string.IsNullOrWhiteSpace(ev.Player.CurrentHint?.Content) || ev.Player.CurrentHint?.Content.StartsWith("Round Time") == true)
            {
                var color = Round.ElapsedTime.TotalMinutes switch
                {
                    >= DangerThreshold => "red",
                    >= ChaosThreshold => "yellow",
                    _ => "white",
                };
                ev.Player.ShowHint($"Round Time: <color={color}>{Round.ElapsedTime:mm\\:ss}</color>", 2);
            }
            yield return Timing.WaitForSeconds(1);
        }
    }


    public static void OnRoundStarted()
    {
        turnedScps = 0;
        didCoinForAll = false;
        CoinActivatedWarhead = null;

        foreach (var light in HasALight.Values)
        {
            light.Destroy();
        }

        HasALight.Clear();
        HasOngoingEffect.Clear();
        DiedToGrenade.Clear();
        ReadyToSwap.Clear();
        GoingToSwap.Clear();

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

        var effects = config.Effects.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // -------------------------------------------------------------------------------------------------------------------------
        // SPECIAL INTERACTIONS BEFORE CHOOSING EFFECT
        if (DiedToGrenade.Values.Any(x => x.Count > 0))
        {
            if (effects.ContainsKey(CoinEffects.ReSpawnSpectators))
                effects[CoinEffects.ReSpawnSpectators] = effects.Values.Max();
        }
        if (Warhead.IsInProgress && ev.Player != CoinActivatedWarhead)
        {
            doesBreak = false;
            if (effects.ContainsKey(CoinEffects.TpToRandom))
                effects[CoinEffects.TpToRandom] = effects.Values.Max();
            if (effects.ContainsKey(CoinEffects.CoinForAll))
                effects[CoinEffects.CoinForAll] = effects.Values.Max();
        }
        // -------------------------------------------------------------------------------------------------------------------------

        var infoCache = new PlayerInfoCache(ev.Player);
        CoinEffects effect = effects.GetRandomKeyByWeight(x => CanHaveEffect(x, infoCache));
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
                    Timing.RunCoroutine(LookLikeScpCoroutine.Coroutine(ev.Player, 60));
                    hintLines.Add(translation.FeelFunny);
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

                    DiedToGrenade.Clear();
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
                    grenade.FuseTime = 4;
                    var grenadeSpawn = grenade.SpawnActive(ev.Player.Position);
                    if (grenadeSpawn != null)
                        DiedToGrenade[grenadeSpawn] = new();
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
                    Warhead.Status = WarheadStatus.InProgress;
                    hintLines.Add(translation.Warhead);
                    CoinActivatedWarhead = ev.Player;
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
                    var team = ev.Player.IsScp ? "by Automatic Security System" : NineTailedFoxAnnouncer.ConvertTeam(ev.Player.Role.Team, ev.Player.UnitName);
                    team = ev.Player.Role.Team == Team.FoundationForces ? ". " + team : " " + team;
                    var announcement = $"contained successfully{team}";
                    Cassie.MessageTranslated($"SCP {withSpace} {announcement}", $"SCP-{withoutSpace} {announcement}".ToUpper());
                    break;
                }
            case CoinEffects.Jail:
                {
                    Timing.RunCoroutine(JailCoroutine.Coroutine(ev.Player, 15));
                    break;
                }
            case CoinEffects.GetALight:
                {
                    Timing.RunCoroutine(GetALightCoroutine.Coroutine(ev.Player, 30));
                    break;
                }
            case CoinEffects.Snapback:
                {
                    Timing.RunCoroutine(SnapbackCoroutine.Coroutine(ev.Player, 15));
                    break;
                }
            case CoinEffects.BecomeSwappable:
                {
                    ReadyToSwap.Add(ev.Player);
                    hintLines.Add(translation.DestabilizedSwap);
                    break;
                }
            case CoinEffects.RemoveSwappable:
                {
                    ReadyToSwap.Remove(ev.Player);
                    hintLines.Add(translation.StabilizedSwap);
                    break;
                }
            case CoinEffects.DoSwap:
                {
                    Timing.RunCoroutine(GoingToSwapCoroutine.Coroutine(ev.Player, 5));
                    break;
                }
        }

        if (doesBreak || ev.Player.Role.Team == Team.SCPs)
        {
            coinBreak(ev.Player, ev.Player.CurrentItem);
        }
        showHint();
    }

    private static bool CanHaveEffect(CoinEffects str, PlayerInfoCache player)
    {
        bool notScp = !player.IsScp;
        var dangerThreshold = Round.ElapsedTime.TotalMinutes >= DangerThreshold;
        var chaosThreshold = Round.ElapsedTime.TotalMinutes >= ChaosThreshold;
        var ongoing = player.OngoingEffect;

        var isOngoingEffect = ongoing != CoinEffects.Nothing;
        var canTp = ongoing != CoinEffects.Jail;

        var gonnaSwap = GoingToSwap.Contains(player.Player);
        var readySwap = ReadyToSwap.Contains(player.Player);

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

            // very chaotic and dangerous effects
            CoinEffects.BecomeSwappable => !readySwap && !gonnaSwap && dangerThreshold,
            CoinEffects.StartWarhead => !Warhead.IsInProgress && !Warhead.IsDetonated && dangerThreshold,
            CoinEffects.BecomeScp => !isOngoingEffect && notScp && dangerThreshold && turnedScps < 2,

            // dependent on other effects
            CoinEffects.StopWarhead => CoinActivatedWarhead != null && Warhead.IsInProgress,
            CoinEffects.RemoveSwappable => readySwap,
            CoinEffects.DoSwap => !readySwap && !gonnaSwap && ReadyToSwap.Any(x => x != player.Player),
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
                + UnityEngine.Random.Range(-1f, 1f) * Vector3.forward * 0.1f;
            Pickup.CreateAndSpawn(ItemType.Coin, item.Position + delta, default, null);
        }
    }

    public static void OnGrenadeExplosion(ExplodingGrenadeEventArgs ev)
    {
        if (!DiedToGrenade.TryGetValue(ev.Projectile, out var players)) 
            return;
        if (ev.TargetsToAffect.Count == 0)
            DiedToGrenade.Remove(ev.Projectile);
        else
            players.UnionWith(ev.TargetsToAffect);
    }

    public static void OnStoppingWarhead(StoppingEventArgs ev)
    {
        CoinActivatedWarhead = null;
    }

    // -------------------------------------------------------------------------------------------------------------------------------------------------
    // -------------------------------------------------------------------------------------------------------------------------------------------------





}
