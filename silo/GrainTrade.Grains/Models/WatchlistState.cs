using System.Dynamic;

namespace GrainTrade.Grains.Models;

public sealed class WatchlistState
{
    public List<string> Symbols {get; set;} = [];
}