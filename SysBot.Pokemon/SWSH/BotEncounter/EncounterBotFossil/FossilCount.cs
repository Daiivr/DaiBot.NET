using PKHeX.Core;
using System;
using static SysBot.Pokemon.FossilSpecies;

namespace SysBot.Pokemon;

public class FossilCount
{
    private int Bird;

    private int Dino;

    private int Drake;

    private int Fish;

    public static FossilCount GetFossilCounts(ReadOnlySpan<byte> itemsBlock)
    {
        var pouch = GetTreasurePouch(itemsBlock);
        return ReadCounts(pouch);
    }

    public int PossibleRevives(FossilSpecies f) => f switch
    {
        Dracozolt => Math.Min(Bird, Drake),
        Arctozolt => Math.Min(Bird, Dino),
        Dracovish => Math.Min(Fish, Drake),
        Arctovish => Math.Min(Fish, Dino),
        _ => throw new ArgumentOutOfRangeException(nameof(f), f, "Las especies fósiles no eran válidas."),
    };

    // Top Half: Select Down for fish if species type == Dracovish || Arctovish
    // Bottom Half: Select Down for dino if species type == Arctozolt || Arctovish
    public bool UseSecondOption1(FossilSpecies f) => Bird != 0 && f is Arctovish or Dracovish;

    public bool UseSecondOption2(FossilSpecies f) => Drake != 0 && f is Arctozolt or Arctovish;

    private static InventoryPouch8 GetTreasurePouch(ReadOnlySpan<byte> itemsBlock)
    {
        var pouch = new InventoryPouch8(InventoryType.Treasure, ItemStorage8SWSH.Instance, 999, 0, 20);
        pouch.GetPouch(itemsBlock);
        return pouch;
    }

    private static FossilCount ReadCounts(InventoryPouch pouch)
    {
        var counts = new FossilCount();
        foreach (var item in pouch.Items)
            counts.SetCount(item.Index, item.Count);
        return counts;
    }

    private void SetCount(int item, int count)
    {
        if (item == 1105)
            Bird = count;
        if (item == 1106)
            Fish = count;
        if (item == 1107)
            Drake = count;
        if (item == 1108)
            Dino = count;
    }
}
