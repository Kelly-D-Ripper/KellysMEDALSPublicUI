#nullable enable
using System;
using System.Collections.Generic;

namespace KellysMEDALSPublicUI;

// Own only a vacant screen entry. Do not overwrite a later owner's registration on teardown.
internal sealed class MfdSlotLease<T> : IDisposable where T : class
{
    private readonly IList<T> screens;
    private readonly T screen;
    private readonly int originalCount;
    internal int Index { get; }
    internal bool Owned => Index < screens.Count && ReferenceEquals(screens[Index], screen);

    private MfdSlotLease(IList<T> screens, int index, T screen)
    {
        this.screens = screens;
        this.screen = screen;
        Index = index;
        originalCount = screens.Count;
        while (screens.Count <= index) screens.Add(null!);
        screens[index] = screen;
    }

    internal static MfdSlotLease<T>? TryClaim(IList<T> screens, int buttonCount, T screen,
                                             Func<int, bool> usable)
    {
        // Take the last empty button, leaving the earlier extras for AIR/TAL regardless of load order.
        // Reserve stock BDF/MAP/HUD positions even while temporarily unpopulated.
        for (int index = buttonCount - 1; index >= 3; index--)
            if ((index >= screens.Count || screens[index] == null) && usable(index))
                return new MfdSlotLease<T>(screens, index, screen);
        return null;
    }

    public void Dispose()
    {
        if (!Owned) return;
        screens[Index] = null!;
        while (screens.Count > originalCount && screens[screens.Count - 1] == null)
            screens.RemoveAt(screens.Count - 1);
    }
}
