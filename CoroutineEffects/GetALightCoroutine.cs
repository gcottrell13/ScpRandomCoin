using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using SCPRandomCoin.API;
using System.Collections.Generic;

using LightToy = Exiled.API.Features.Toys.Light;
using PrimitiveToy = Exiled.API.Features.Toys.Primitive;

namespace SCPRandomCoin.CoroutineEffects;

internal class GetALightCoroutine
{
    public static Dictionary<Player, LightToy> HasALight = new();

    public static void Reset()
    {
        foreach (var light in HasALight.Values)
        {
            light.Destroy();
        }
        HasALight.Clear();
    }

    public static IEnumerator<float> Coroutine(Player player, int waitSeconds)
    {
        var light = LightToy.Create(player.Position);
        light.MovementSmoothing = 60;
        light.Intensity = 10;
        light.Base.transform.SetParent(player.Transform);
        HasALight[player] = light;
        player.ChangeAppearance(RoleTypeId.Spectator);
        EffectHandler.HasOngoingEffect[player] = CoinEffects.GetALight;
        player.EnableEffect(EffectType.Ghostly, waitSeconds);
        yield return Timing.WaitForSeconds(waitSeconds);
        light.Destroy();
        HasALight.Remove(player);
        EffectHandler.HasOngoingEffect.Remove(player);
        if (player.IsAlive)
            player.ChangeAppearance(player.Role);
    }
}
