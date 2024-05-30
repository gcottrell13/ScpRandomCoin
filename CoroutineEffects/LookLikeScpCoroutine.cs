using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using SCPRandomCoin.API;
using System.Collections.Generic;

namespace SCPRandomCoin.CoroutineEffects;

internal class LookLikeScpCoroutine
{
    public static IEnumerator<float> Coroutine(Player player, int waitSeconds)
    {
        var scp = new[] {
            RoleTypeId.Scp049,
            RoleTypeId.Scp096,
            RoleTypeId.Scp3114,
            RoleTypeId.Scp106,
            RoleTypeId.Scp939,
            RoleTypeId.Scp173,
        }.GetRandomValue();
        player.ChangeAppearance(scp);
        EffectHandler.HasOngoingEffect[player] = CoinEffects.LookLikeScp;
        yield return Timing.WaitForSeconds(waitSeconds);
        EffectHandler.HasOngoingEffect.Remove(player);
        player.ChangeAppearance(player.Role);
    }
}
