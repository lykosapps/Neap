namespace Neap.Core.Hid;

/// <summary>One top-level collection of a HID device, and how long its reports are.</summary>
/// <param name="UsagePage">The collection's usage page.</param>
/// <param name="InputLength">Its longest input report in bytes, report id included.</param>
/// <param name="OutputLength">Its longest output report in bytes, report id included.</param>
/// <param name="FeatureLength">Its longest feature report in bytes, report id included; 0 for none.</param>
public sealed record CollectionReports(ushort UsagePage, int InputLength, int OutputLength, int FeatureLength);

/// <summary>Reads a HID report descriptor for what Windows reports as a collection's capabilities.</summary>
/// <remarks>
/// <para>
/// Windows gives each top-level collection a device of its own and says how
/// long its reports are. Linux gives one device per USB interface and hands
/// over the raw descriptor, so the same facts are read from that. Lengths
/// count the report id byte, as Windows' do, whether or not the device uses
/// report ids.
/// </para>
/// <para>
/// A descriptor that does not parse is refused whole. Reading part of one
/// would turn "cannot read" into "has no control collection", and a device
/// would vanish without a word.
/// </para>
/// </remarks>
public static class ReportDescriptor
{
    private const int Main = 0, Global = 1;

    private const int InputTag = 0x8, OutputTag = 0x9, CollectionTag = 0xA, FeatureTag = 0xB, EndCollectionTag = 0xC;
    private const int UsagePageTag = 0x0, ReportSizeTag = 0x7, ReportIdTag = 0x8, ReportCountTag = 0x9, PushTag = 0xA, PopTag = 0xB;

    /// <summary>The top-level collections in a descriptor, in order.</summary>
    /// <exception cref="FormatException">The descriptor is cut short or its collections do not nest.</exception>
    public static IReadOnlyList<CollectionReports> Collections(ReadOnlySpan<byte> descriptor)
    {
        var found = new List<CollectionReports>();
        var globals = new Globals();
        var saved = new Stack<Globals>();
        int depth = 0;
        ushort topPage = 0;
        // Bits per report id and kind, for the top-level collection being read.
        var bits = new Dictionary<(byte Id, int Tag), int>();

        int at = 0;
        while (at < descriptor.Length)
        {
            byte prefix = descriptor[at++];
            if (prefix == 0xFE)
            {
                // A long item: its size is in the next byte. None is defined, so skip it.
                if (at + 2 > descriptor.Length) throw Cut(at);
                at += 2 + descriptor[at];
                continue;
            }

            int size = (prefix & 0x3) == 3 ? 4 : prefix & 0x3;
            int type = (prefix >> 2) & 0x3;
            int tag = prefix >> 4;
            if (at + size > descriptor.Length) throw Cut(at);
            uint data = 0;
            for (int i = 0; i < size; i++) data |= (uint)descriptor[at + i] << (8 * i);
            at += size;

            if (type == Global)
            {
                switch (tag)
                {
                    case UsagePageTag: globals.UsagePage = (ushort)data; break;
                    case ReportSizeTag: globals.ReportSize = (int)data; break;
                    case ReportIdTag: globals.ReportId = (byte)data; break;
                    case ReportCountTag: globals.ReportCount = (int)data; break;
                    case PushTag: saved.Push(globals); break;
                    case PopTag:
                        if (saved.Count == 0) throw new FormatException("the descriptor pops more than it pushed");
                        globals = saved.Pop();
                        break;
                }
            }
            else if (type == Main)
            {
                switch (tag)
                {
                    case CollectionTag:
                        if (depth == 0)
                        {
                            topPage = globals.UsagePage;
                            bits.Clear();
                        }
                        depth++;
                        break;
                    case EndCollectionTag:
                        if (depth == 0) throw new FormatException("the descriptor ends a collection it never began");
                        if (--depth == 0)
                            found.Add(new CollectionReports(topPage,
                                Longest(bits, InputTag), Longest(bits, OutputTag), Longest(bits, FeatureTag)));
                        break;
                    case InputTag or OutputTag or FeatureTag:
                        var key = (globals.ReportId, tag);
                        bits[key] = bits.GetValueOrDefault(key) + globals.ReportSize * globals.ReportCount;
                        break;
                }
            }
        }

        if (depth != 0) throw new FormatException("the descriptor leaves a collection open");
        return found;
    }

    private static int Longest(Dictionary<(byte Id, int Tag), int> bits, int tag)
    {
        int longest = 0;
        foreach (var ((_, kind), count) in bits)
            if (kind == tag) longest = Math.Max(longest, (count + 7) / 8 + 1);
        return longest;
    }

    private static FormatException Cut(int at) => new($"the descriptor is cut short at byte {at}");

    private struct Globals
    {
        public ushort UsagePage;
        public int ReportSize;
        public int ReportCount;
        public byte ReportId;
    }
}
