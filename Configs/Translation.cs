using Exiled.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCPRandomCoin.Configs;

internal class Translation : ITranslation
{
    public string GetItem { get; private set; } = "You got {item}.";
    public string LoseItem { get; private set; } = "You lost {item}.";
    public string CoinBreak { get; private set; } = "The coin was used too much and broke.";
    public string Nothing { get; private set; } = "The coin decided to do nothing.";
    public string Heal { get; private set; } = "You were fully healed!";
    public string Tp { get; private set; } = "The coin teleported you.";
    public string Grenade { get; private set; } = "<size=30>GRENADE!</size>";
    public string OneHp { get; private set; } = "The coin says: Try not to die.";
    public string LookLikeScp { get; private set; } = "You feel kind of funny.";
    public string Respawn { get; private set; } = "You brought back {count} players from the dead!";
    public string Respawned { get; private set; } = "{name} brought you back using their coin.";
    public string Wide { get; private set; } = "You feel kind of funny...";
}
