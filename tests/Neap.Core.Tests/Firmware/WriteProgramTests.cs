using System.Security.Cryptography;
using Neap.Core.Firmware;

namespace Neap.Core.Tests.Firmware;

public class WriteProgramTests
{
    // SHA-256 of what SP2Tools' brom_sector_write.py assembles (keystone) for the same blocks.
    [Fact]
    public void TheProvenRestoreProgramComesOutByteForByte() =>
        Assert.Equal("C6D5C1F650C62D3D682B5D4B1C56C128EC2F7B3559F4BB9CA5F04475F8FDECD3", Hash(WriteProgram.Build(
            [(0x08100000, null), (0x08191000, WriteProgram.PayloadAddress), (0x080D7000, WriteProgram.PayloadAddress + 0x1000)])));

    [Fact]
    public void TheNoiseCyclesCodeThenItsCallComesOutAsSp2ToolsBuildsIt() =>
        Assert.Equal("0E724A43579C23A49F52B38AC20EC44469BE70EC40B7D4A84D99E3ACE6B57D8D", Hash(WriteProgram.For([0x08349000, 0x08232000])));

    [Fact]
    public void TakingTheNoiseCycleOffComesOutAsSp2ToolsBuildsIt() =>
        Assert.Equal("A4DB9348DC3BAA4626A9DE29EB80DB9E60A1E2FECDD15658522F4628823ED7AD", Hash(WriteProgram.For([0x08232000, 0x08349000])));

    [Fact]
    public void OneSectionComesOutAsSp2ToolsBuildsIt() =>
        Assert.Equal("14CAD300BDD0FAF20F9BE0D0E57209CA38DDDD8F39322533D60AAB3B9C70ECF0", Hash(WriteProgram.For([0x082F9000])));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void AProgramWritesOneOrTwoSections(int count) =>
        Assert.Throws<ArgumentException>(() => WriteProgram.For(Enumerable.Repeat(0x08349000, count).ToList()));

    [Fact]
    public void ASectionMustStartOnASectionBoundary() =>
        Assert.Throws<ArgumentException>(() => WriteProgram.For([0x08349004]));

    private static string Hash(byte[] program) => Convert.ToHexString(SHA256.HashData(program));
}
