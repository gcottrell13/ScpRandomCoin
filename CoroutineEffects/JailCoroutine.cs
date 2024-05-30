using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using MEC;
using PlayerRoles;
using SCPRandomCoin.API;
using System.Collections.Generic;
using UnityEngine;

namespace SCPRandomCoin.CoroutineEffects;

internal class JailCoroutine
{
    public static readonly List<List<ItemType>> JailItems = new()
    {
        new()
        {
            ItemType.Medkit,
            ItemType.Medkit,
            ItemType.Adrenaline,
            ItemType.Adrenaline,
            ItemType.Painkillers,
            ItemType.Painkillers,
            ItemType.Painkillers,
            ItemType.None,
        },
        new()
        {
            ItemType.KeycardJanitor,
            ItemType.KeycardJanitor,
            ItemType.KeycardJanitor,
            ItemType.Medkit,
            ItemType.KeycardJanitor,
            ItemType.KeycardJanitor,
            ItemType.None,
        },
    };

    public static IEnumerator<float> Coroutine(Player player, int waitSeconds)
    {
        var oldPos = player.Position;
        var newPos = RoleTypeId.Tutorial.GetRandomSpawnLocation().Position;
        player.Position = newPos;
        EffectHandler.HasOngoingEffect[player] = CoinEffects.Jail;
        var spawnedItems = new List<Pickup>();
        var itemTypes = JailItems.GetRandomValue();
        itemTypes.ShuffleList();

        for (int i = 0; i < itemTypes.Count; i++)
        {
            var type = itemTypes[i];
            if (type == ItemType.None && SCPRandomCoin.Singleton != null)
                type = SCPRandomCoin.Singleton.Config.ItemList.GetRandomKeyByWeight();

            spawnedItems.Add(Pickup.CreateAndSpawn(
                type,
                newPos + (Quaternion.Euler(0, i * 360f / itemTypes.Count, 0) * Vector3.forward) + Vector3.up * 0.5f,
                Quaternion.Euler(UnityEngine.Random.Range(0, 180), UnityEngine.Random.Range(0, 180), UnityEngine.Random.Range(0, 180))
             ));
        }
        for (int i = 0; i < waitSeconds; i++)
        {
            player.ShowHint($"You have {waitSeconds - i} seconds left here", 1.1f);
            yield return Timing.WaitForSeconds(1f);
        }
        EffectHandler.HasOngoingEffect.Remove(player);
        player.Position = oldPos;
        foreach (var item in spawnedItems)
            if (item.IsSpawned) item.Destroy();
    }
}
