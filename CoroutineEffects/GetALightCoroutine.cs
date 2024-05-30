using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using SCPRandomCoin.API;
using System.Collections.Generic;

using LightToy = Exiled.API.Features.Toys.Light;

namespace SCPRandomCoin.CoroutineEffects;

internal class GetALightCoroutine
{

    public static IEnumerator<float> Coroutine(Player player, int waitSeconds)
    {
        var light = LightToy.Create(player.Position);
        light.MovementSmoothing = 60;
        light.Intensity = 10;
        light.Base.transform.SetParent(player.Transform);
        EventHandlers.HasALight[player] = light;
        player.ChangeAppearance(RoleTypeId.Spectator);
        EventHandlers.HasOngoingEffect[player] = CoinEffects.GetALight;
        player.EnableEffect(EffectType.Ghostly, waitSeconds);
        yield return Timing.WaitForSeconds(waitSeconds);
        light.Destroy();
        EventHandlers.HasALight.Remove(player);
        EventHandlers.HasOngoingEffect.Remove(player);
        player.ChangeAppearance(player.Role);
    }
}
