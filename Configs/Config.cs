using Exiled.API.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using SCPRandomCoin.API;

namespace SCPRandomCoin.Configs;

internal class Config : IConfig
{
    public bool IsEnabled { get; set; }
    public bool Debug { get; set; }

    [Description("Effects to subject the coin flipper to, should they get heads. key is effect name, value is the relative weight.")]
    public Dictionary<string, float> Effects { get; private set; } = new()
    {    
        { nameof(CoinEffects.Nothing), 1 },
        { nameof(CoinEffects.OneHp), 10 },
        { nameof(CoinEffects.TpToScp), 10 },
        { nameof(CoinEffects.BecomeScp), 10 },
        { nameof(CoinEffects.GetPoorItem), 10 },
        { nameof(CoinEffects.GetGoodItem), 5 },
        { nameof(CoinEffects.LoseItem), 10 },
        { nameof(CoinEffects.Heal), 10 },
        { nameof(CoinEffects.TpToRandom), 10 },
    };

    [Description("Should the coin have a chance to break even if it didn't do anything?")]
    public bool CoinBreakOnTails { get; private set; } = false;

    [Description("The % chance for the coin to break")]
    public int CoinBreakPercent { get; private set; } = 50;


}
