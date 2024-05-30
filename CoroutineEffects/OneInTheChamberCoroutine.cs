using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using MEC;
using SCPRandomCoin.API;
using System.Collections.Generic;
using PlayerEvent = Exiled.Events.Handlers.Player;

namespace SCPRandomCoin.CoroutineEffects;

internal class OneInTheChamberCoroutine
{
    public class PlayerRecord
    {
        public Player Player;
        public Firearm Weapon;
        public int ShotsHit;
        public bool Playing;

        public PlayerRecord(Player player, Firearm weapon)
        {
            Player = player;
            Weapon = weapon;
        }
    }

    public static Dictionary<Player, PlayerRecord> OitcPlayers = new();

    public static IEnumerator<float> Coroutine(Player player)
    {
        var translation = SCPRandomCoin.Singleton?.Translation;
        if (translation == null)
            yield break;

        PlayerEvent.Shot += OnShot;
        PlayerEvent.Hurting += OnHurting;
        PlayerEvent.ChangingItem += OnChangingItem;
        PlayerEvent.SearchingPickup += OnSearchingPickup;
        PlayerEvent.DroppingItem += OnDroppingItem;

        EffectHandler.HasOngoingEffect[player] = CoinEffects.OneInTheChamber;

        var weapon = (Firearm)player.AddItem(ItemType.GunRevolver);
        weapon.Ammo = 1;
        player.CurrentItem = weapon;

        var info = new PlayerRecord(player, weapon)
        {
            ShotsHit = 0,
            Playing = true,
        };
        OitcPlayers[player] = info;

        while (info.Playing && !Round.IsEnded)
        {
            var comboMeter = info.ShotsHit switch
            {
                0 => "",
                _ => $"<size={info.ShotsHit * 3 + 20}><color=yellow>x{info.ShotsHit}</color></size>",
            };

            player.ShowHint($"{comboMeter}\n{translation.OneInTheChamber}");

            yield return Timing.WaitForSeconds(1f);
        }

        if (!Round.IsEnded)
        {
            player.RemoveItem(weapon);
            player.ShowHint(translation.OneInTheChamberFinish.Replace("{count}", info.ShotsHit.ToString()));

            EffectHandler.HasOngoingEffect.Remove(player);
        }

        OitcPlayers.Remove(player);
        PlayerEvent.Shot -= OnShot;
        PlayerEvent.Hurting -= OnHurting;
        PlayerEvent.ChangingItem -= OnChangingItem;
        PlayerEvent.SearchingPickup -= OnSearchingPickup;
        PlayerEvent.DroppingItem -= OnDroppingItem;
    }

    public static IEnumerator<float> OnShot(ShotEventArgs ev)
    {
        if (OitcPlayers.ContainsKey(ev.Player) == false)
            yield break;

        if (ev.Target == null)
        {
            OitcPlayers[ev.Player].Playing = false;
        }
        else
        {
            yield return Timing.WaitForSeconds(0.5f);
            ev.Firearm.Ammo = 1;
            OitcPlayers[ev.Player].ShotsHit++;
        }
    }
    public static void OnHurting(HurtingEventArgs ev)
    {
        if (OitcPlayers.ContainsKey(ev.Attacker) == false)
            return;
        ev.Amount = 150;
    }

    public static void OnChangingItem(ChangingItemEventArgs ev)
    {
        if (OitcPlayers.ContainsKey(ev.Player) == false)
            return;
        ev.IsAllowed = false;
    }
    public static void OnSearchingPickup(SearchingPickupEventArgs ev)
    {
        if (OitcPlayers.ContainsKey(ev.Player) == false)
            return;
        ev.IsAllowed = false;
    }
    public static void OnDroppingItem(DroppingItemEventArgs ev)
    {
        if (OitcPlayers.ContainsKey(ev.Player) == false)
            return;
        ev.IsAllowed = false;
    }
}
