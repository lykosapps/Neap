using System.Buffers.Binary;

namespace Neap.Core.Presets;

/// <summary>
/// The headset's stored game equaliser: the 186-byte record at NVKey 0x500B
/// that holds each band's frequency, width, gain and filter, and the switch
/// for the stock gain correction.
/// </summary>
/// <remarks>
/// <para>
/// Layout (SP2Tools, read from the firmware and one headset's saved record):
/// a 4-byte header that counts ten bands, ten bands of 18 bytes each (the
/// frequency, width, gain and sample rate as little-endian floats, then the
/// filter as a little-endian 16-bit number), and two flag bytes. The first flag
/// byte, at 184, is 1 as shipped: while it is set, a gain change passes through a
/// correction made for the stock frequencies, which leaves the gains up to
/// about 2 dB out once a band has moved.
/// </para>
/// <para>
/// Nothing is parsed that does not fit: a record of another length, another
/// band count, an unknown filter or a flag that is neither 0 nor 1 is a failure
/// and not a record, so a reading that cannot be trusted is never written back.
/// </para>
/// </remarks>
public sealed class GameEqRecord
{
    /// <summary>The key the record is stored under.</summary>
    public const ushort Key = 0x500B;

    /// <summary>The record's length in bytes.</summary>
    public const int Length = 186;

    private const int HeaderLength = 4;
    private const int BandLength = 18;
    private const int FlagOffset = HeaderLength + (BandLength * BandWrites.Bands);

    private readonly byte[] _bytes;

    private GameEqRecord(byte[] bytes) => _bytes = bytes;

    /// <summary>The record as the headset ships it: flat octave bells at the stock frequencies, the gain correction on.</summary>
    public static GameEqRecord Shipped { get; } = BuildShipped();

    /// <summary>The sample rate the shipped record carries; the firmware overwrites it with the live stream's.</summary>
    private const float ShippedSampleRate = 48000f;

    private static GameEqRecord BuildShipped()
    {
        var bytes = new byte[Length];
        Header.CopyTo(bytes);
        for (int band = 0; band < BandWrites.Bands; band++)
        {
            var at = bytes.AsSpan(HeaderLength + (band * BandLength), BandLength);
            BinaryPrimitives.WriteSingleLittleEndian(at, (float)StockBands.Game[band].Hertz);
            BinaryPrimitives.WriteSingleLittleEndian(at[4..], (float)StockBands.Q);
            BinaryPrimitives.WriteSingleLittleEndian(at[8..], 0f);
            BinaryPrimitives.WriteSingleLittleEndian(at[12..], ShippedSampleRate);
            BinaryPrimitives.WriteUInt16LittleEndian(at[16..], (ushort)BandType.Peaking);
        }

        bytes[FlagOffset] = 1;
        return new GameEqRecord(bytes);
    }

    /// <summary>The record's first four bytes, as a reply's reader looks for them.</summary>
    public static ReadOnlySpan<byte> Header => [0x0A, 0x00, 0x00, 0x00];

    /// <summary>The record as bytes.</summary>
    public ReadOnlySpan<byte> Bytes => _bytes;

    /// <summary>Whether the stock gain correction is on, as the headset ships it.</summary>
    public bool GainCorrectionOn => _bytes[FlagOffset] == 1;

    /// <summary>The ten bands as the record holds them, each in use.</summary>
    public IReadOnlyList<ParametricBand> Bands
    {
        get
        {
            var bands = new ParametricBand[BandWrites.Bands];
            for (int band = 0; band < bands.Length; band++)
            {
                var at = _bytes.AsSpan(HeaderLength + (band * BandLength), BandLength);
                bands[band] = new ParametricBand(
                    (BandType)BinaryPrimitives.ReadUInt16LittleEndian(at[16..]),
                    BinaryPrimitives.ReadSingleLittleEndian(at),
                    BinaryPrimitives.ReadSingleLittleEndian(at[4..]),
                    BinaryPrimitives.ReadSingleLittleEndian(at[8..]),
                    InUse: true);
            }

            return bands;
        }
    }

    /// <summary>The record from bytes the headset gave, or null when they are not a record Neap understands.</summary>
    /// <param name="bytes">The 186 bytes.</param>
    public static GameEqRecord? Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length || !bytes[..HeaderLength].SequenceEqual(Header)) return null;
        if (bytes[FlagOffset] > 1) return null;
        for (int band = 0; band < BandWrites.Bands; band++)
        {
            var at = bytes.Slice(HeaderLength + (band * BandLength), BandLength);
            if (!Enum.IsDefined((BandType)BinaryPrimitives.ReadUInt16LittleEndian(at[16..]))) return null;
            if (!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(at)) || !float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(at[4..]))
                || !float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(at[8..])))
            {
                return null;
            }
        }

        return new GameEqRecord(bytes.ToArray());
    }

    /// <summary>The same record with the stock gain correction on or off and every other byte unchanged.</summary>
    /// <param name="on">Whether the correction is on.</param>
    public GameEqRecord WithGainCorrection(bool on)
    {
        var copy = (byte[])_bytes.Clone();
        copy[FlagOffset] = on ? (byte)1 : (byte)0;
        return new GameEqRecord(copy);
    }

    /// <summary>Whether another record is the same byte for byte.</summary>
    /// <param name="other">The record to compare.</param>
    public bool Matches(GameEqRecord other) => _bytes.AsSpan().SequenceEqual(other._bytes);
}
