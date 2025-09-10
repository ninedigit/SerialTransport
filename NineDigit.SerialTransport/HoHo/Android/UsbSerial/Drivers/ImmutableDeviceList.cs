#if ANDROID
using System.Collections;
using System.Collections.Immutable;

namespace Hoho.Android.UsbSerial.Drivers;

public record ImmutableDeviceList : IEnumerable<ImmutableDeviceList.Entry>
{
    private ImmutableHashSet<Entry> Entries { get; }

    public ImmutableDeviceList()
    {
        Entries = ImmutableHashSet<Entry>.Empty;
    }

    public ImmutableDeviceList(Dictionary<int, int[]> list)
    {
        Entries = list.SelectMany(i => i.Value.Select(j => new Entry(i.Key, j))).ToImmutableHashSet();
    }

    public IEnumerator<Entry> GetEnumerator()
        => Entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public record Entry(int VendorId, int ProductId);
}
#endif