using Neap.Core.Firmware;
using Neap.Core.Pretend;

namespace Neap.Core.Tests.Firmware;

public class FirmwareBackupTests
{
    private const string Build = "4.107.703.0";
    private static readonly int Light = FirmwareEdit.LightOffWhenDisconnected.Sectors[0];
    private static readonly int Call = FirmwareEdit.NoiseCycle.Sectors[1];
    private static readonly DateTime Noon = new(2026, 11, 5, 12, 0, 0);

    private static FirmwareBackup At(int sector, int minutes, string build = Build) =>
        new(FirmwareBackup.FileName(Noon.AddMinutes(minutes), sector, build), Noon.AddMinutes(minutes), sector, build);

    [Fact]
    public void AFileNameSaysWhatTheBackupHolds()
    {
        string name = FirmwareBackup.FileName(Noon, Light, Build);

        Assert.Equal(new FirmwareBackup(name, Noon, Light, Build), FirmwareBackup.Of(name));
    }

    [Fact]
    public void AFileNotNamedAsABackupIsNotOne() =>
        Assert.Null(FirmwareBackup.Of("my headset.bin"));

    [Fact]
    public void OnlyTheNewestOfEachSectionIsListedNewestFirst()
    {
        var newestLight = At(Light, 30);
        var newestCall = At(Call, 10);

        var listed = FirmwareBackup.NewestOfEach([At(Light, 0), newestCall, newestLight, At(Light, 20)]);

        Assert.Equal([newestLight, newestCall], listed);
    }

    [Fact]
    public void ABackupOfOtherFirmwareIsListedApart()
    {
        var older = At(Light, 0, "4.107.702.0");

        Assert.Contains(older, FirmwareBackup.NewestOfEach([older, At(Light, 10)]));
    }

    [Fact]
    public void TheNewestOfASectionIsFoundForItsFirmwareOnly()
    {
        var mine = At(Light, 0);

        Assert.Equal(mine, FirmwareBackup.NewestOf([mine, At(Light, 10, "4.107.702.0"), At(Call, 20)], Light, Build));
    }

    [Fact]
    public void TheFactorySectionOrOneWithNeapsEditFits()
    {
        var flash = new PretendFlash();
        byte[] edited = flash.Read(Light);
        var change = FirmwareEdit.LightOffWhenDisconnected.Changes[0];
        change.Active.ToArray().CopyTo(edited, change.Address - Light);

        Assert.Equal(BackupFit.Fits, FirmwareBackup.Judge(At(Light, 0), flash.Read(Light), Build, flash.FactorySections));
        Assert.Equal(BackupFit.Fits, FirmwareBackup.Judge(At(Light, 0), edited, Build, flash.FactorySections));
    }

    [Fact]
    public void TheCodesFactoryBackupIsRefusedWhileTheLastReadHasItsCallOn()
    {
        var flash = new PretendFlash();
        var cycle = FirmwareEdit.NoiseCycle;
        int code = cycle.Sectors[0];
        var on = cycle.Changes.ToDictionary(change => change.Address, change => change.Active.ToArray());
        var off = cycle.Changes.ToDictionary(change => change.Address, change => change.Factory.ToArray());

        Assert.Equal(BackupFit.LeavesHalfOn, FirmwareBackup.Judge(At(code, 0), flash.Read(code), Build, flash.FactorySections, on));
        Assert.Equal(BackupFit.Fits, FirmwareBackup.Judge(At(code, 0), flash.Read(code), Build, flash.FactorySections, off));
        Assert.Equal(BackupFit.Fits, FirmwareBackup.Judge(At(code, 0), flash.Read(code), Build, flash.FactorySections));
    }

    [Fact]
    public void ASectionHoldingAnythingElseIsRefused()
    {
        var flash = new PretendFlash();
        byte[] other = flash.Read(Light);
        other[0] ^= 0xFF;

        Assert.Equal(BackupFit.Unknown, FirmwareBackup.Judge(At(Light, 0), other, Build, flash.FactorySections));
    }

    [Fact]
    public void OtherFirmwareNoHeadsetOrNoBackupAreRefusedBeforeTheBytesAreLookedAt()
    {
        var flash = new PretendFlash();
        byte[] section = flash.Read(Light);

        Assert.Equal(BackupFit.OtherFirmware, FirmwareBackup.Judge(At(Light, 0, "4.107.702.0"), section, Build, flash.FactorySections));
        Assert.Equal(BackupFit.NoHeadset, FirmwareBackup.Judge(At(Light, 0), section, null, flash.FactorySections));
        Assert.Equal(BackupFit.NotABackup, FirmwareBackup.Judge(null, section, Build, flash.FactorySections));
        Assert.Equal(BackupFit.NotABackup, FirmwareBackup.Judge(At(Light, 0), section.AsSpan(0, 100), Build, flash.FactorySections));
    }
}
