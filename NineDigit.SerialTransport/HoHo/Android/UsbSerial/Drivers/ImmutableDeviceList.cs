#if ANDROID
using System.Collections;
using System.Collections.Immutable;
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global

namespace Hoho.Android.UsbSerial.Drivers;

public record ImmutableDeviceList : IEnumerable<ImmutableDeviceList.Entry>
{
    private ImmutableHashSet<Entry> Entries { get; }

    public ImmutableDeviceList()
    {
        Entries = ImmutableHashSet<Entry>.Empty;
    }

    private ImmutableDeviceList(ImmutableHashSet<Entry> entries)
    {
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    public ImmutableDeviceList(Dictionary<int, int[]> list)
    {
        Entries = list.SelectMany(i => i.Value.Select(j => new Entry(i.Key, j))).ToImmutableHashSet();
    }

    public IEnumerator<Entry> GetEnumerator()
        => Entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public static ImmutableDeviceList Create(params Entry[] entries)
    {
        if (entries is null)
            throw new ArgumentNullException(nameof(entries));
        
        var entryHashSet = entries.ToImmutableHashSet();
        var deviceList = new ImmutableDeviceList(entryHashSet);

        return deviceList;
    }

    public record Entry(int VendorId, int ProductId);
}
#endif