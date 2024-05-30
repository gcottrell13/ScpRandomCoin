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

        public IEnumerator<float> OnShot(ShotEventArgs ev)
        {
            if (ev.Player != Player)
                yield break;

            if (ev.Target == null)
            {
                Playing = false;
            }
            else
            {
                ShotsHit++;
                yield return Timing.WaitForSeconds(0.5f);
                Weapon.Ammo = 1;
            }
        }
        public void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Attacker != Player)
                return;
            ev.Amount = 150;
        }

        public void OnChangingItem(ChangingItemEventArgs ev)
        {
            if (ev.Player != Player)
                return;
            ev.IsAllowed = false;
        }
        public void OnSearchingPickup(SearchingPickupEventArgs ev)
        {
            if (ev.Player != Player)
                return;
            ev.IsAllowed = false;
        }
        public void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (ev.Player != Player)
                return;
            ev.IsAllowed = false;
        }
    }

    public static Dictionary<Player, PlayerRecord> OitcPlayers = new();

    public static IEnumerator<float> Coroutine(Player player)
    {
        var translation = SCPRandomCoin.Singleton?.Translation;
        if (translation == null || OitcPlayers.ContainsKey(player))
            yield break;

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

        PlayerEvent.Shot += info.OnShot;
        PlayerEvent.Hurting += info.OnHurting;
        PlayerEvent.ChangingItem += info.OnChangingItem;
        PlayerEvent.SearchingPickup += info.OnSearchingPickup;
        PlayerEvent.DroppingItem += info.OnDroppingItem;

        while (player.IsAlive && info.Playing && !Round.IsEnded && player.CurrentItem == weapon)
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
        PlayerEvent.Shot -= info.OnShot;
        PlayerEvent.Hurting -= info.OnHurting;
        PlayerEvent.ChangingItem -= info.OnChangingItem;
        PlayerEvent.SearchingPickup -= info.OnSearchingPickup;
        PlayerEvent.DroppingItem -= info.OnDroppingItem;
    }

}
