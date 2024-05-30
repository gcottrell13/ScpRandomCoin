using Exiled.API.Features;
using MEC;
using SCPRandomCoin.API;
using System.Collections.Generic;

namespace SCPRandomCoin.CoroutineEffects;

internal class SnapbackCoroutine
{
    public static IEnumerator<float> Coroutine(Player player, int waitSeconds)
    {
        var state = new PlayerState(player);
        EffectHandler.HasOngoingEffect[player] = CoinEffects.Snapback;
        for (int i = 0; i < waitSeconds; i++)
        {
            player.ShowHint($"<size={10 + i * 3}>Time snaps back in {waitSeconds - i} seconds</size>", 1.1f);
            yield return Timing.WaitForSeconds(1f);
        }
        state.Apply(player);
        EffectHandler.HasOngoingEffect.Remove(player);
    }
}
