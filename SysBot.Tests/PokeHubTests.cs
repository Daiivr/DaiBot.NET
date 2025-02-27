using FluentAssertions;
using PKHeX.Core;
using SysBot.Pokemon;
using Xunit;

namespace SysBot.Tests;

public class PokeHubTests
{
    [Fact]
    public void TestHub() => Test<PK8>();

    private static void Test<T>() where T : PKM, new()
    {
        var cfg = new PokeTradeHubConfig { Distribution = { DistributeWhileIdle = true } };
        var hub = new PokeTradeHub<T>(cfg);

        var pool = hub.Ledy.Pool;
        var a = new T { Species = 5 };
        pool.Add(a);

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var trade = hub.Queues.TryDequeue(PokeRoutineType.FlexTrade, out _, out _, null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        trade.Should().BeFalse();

        var ledy = hub.Queues.TryDequeueLedy(out var detail);
        ledy.Should().BeTrue();
        detail.TradeData.Should().Be(a);
    }
}
