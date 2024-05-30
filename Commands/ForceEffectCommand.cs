using CommandSystem;
using Exiled.API.Features;
using SCPRandomCoin.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils;

namespace SCPRandomCoin.Commands;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
internal class ForceEffectCommand : ICommand
{
    public string Command => "coin-effect";

    public string[] Aliases => Array.Empty<string>();

    public string Description => $"{Command} [CoinEffects] <player | *>\nForces a coin effect on a player.";

    public static readonly Dictionary<string, CoinEffects> EffectDict = Enum.GetNames(typeof(CoinEffects))
        .ToDictionary(name => name.ToLower(), name => (CoinEffects)Enum.Parse(typeof(CoinEffects), name));

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        List<Player> players = new();
        if (arguments.Count == 1)
        {
            players.Add(Player.Get(sender));
        }
        else if (arguments.Count == 2)
        {
            players.AddRange(
                arguments.ElementAt(0) == "*" ? Player.List : RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out var newargs).Select(Player.Get)
            );
        }
        else
        {
            response = $"Invalid number of parameters.\n{Description}";
            return false;
        }

        if (!EffectDict.TryGetValue(arguments.ElementAt(0).ToLower(), out var effect))
        {
            response = "Invalid effect";
            return false;
        }

        foreach (var player in players)
        {
            EffectHandler.ForceCoinEffect(player, effect);
        }

        response = $"Affected {players.Count} players";
        return true;
    }
}
