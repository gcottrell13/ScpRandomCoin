using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using SCPRandomCoin.API;
using SCPRandomCoin.Commands;
using System.Collections.Generic;
using System.Linq;

namespace SCPRandomCoin.CoroutineEffects;

internal class GoingToSwapCoroutine
{
    public static IEnumerator<float> Coroutine(Player player, int waitSeconds)
    {
        EventHandlers.GoingToSwap.Add(player);
        for (int i = 0; i < waitSeconds; i++)
        {
            if (EventHandlers.GoingToSwap.Contains(player) == false)
            {
                // the StableCommand could remove the player from this list.
                player.ShowHint("");
                yield break;
            }
            if (!EventHandlers.ReadyToSwap.Any(x => x != player))
            {
                break;
            }

            player.ShowHint(SCPRandomCoin.Singleton?.Translation.GoingToSwap.Format(new()
            {
                { "time", waitSeconds - i },
                { "command", StableCommand.ShortAlias },
            }));

            yield return Timing.WaitForSeconds(1);
        }

        EventHandlers.GoingToSwap.Remove(player);
        var target = EventHandlers.ReadyToSwap.Where(x => x != player).GetRandomValue();
        if (target == null)
        {
            player.ShowHint(SCPRandomCoin.Singleton?.Translation.CancelSwap);
            yield break;
        }

        player.ShowHint("");
        EventHandlers.ReadyToSwap.Remove(target);
        var p = new PlayerState(player);
        var t = new PlayerState(target);
        p.Apply(target);
        t.Apply(player);
    }
}
