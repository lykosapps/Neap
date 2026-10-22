using Neap.Core.Firmware;

namespace Neap.Core.Tests.Firmware;

public class FirmwareEditTests
{
    private static readonly FirmwareEdit Edit = FirmwareEdit.NoiseCycle;
    private const string Build = "4.107.703.0";

    private static EditReadiness Ready(
        bool connected = true, string? build = Build, int? battery = 80, bool swarm = false) =>
        new(connected, build, battery, swarm);

    [Fact]
    public void TheNoiseCycleIsMadeForTheStealthProII() =>
        Assert.Equal([Edit], FirmwareEdit.For(HeadsetModels.StealthProII));

    [Fact]
    public void TheAtlasAirHasNoEdits() => Assert.Empty(FirmwareEdit.For(HeadsetModels.AtlasAir));

    [Fact]
    public void AnUnknownHeadsetHasNoEdits() => Assert.Empty(FirmwareEdit.For(null));

    [Fact]
    public void EveryEditHasABitOfItsOwn() =>
        Assert.Equal(FirmwareEdit.All.Count, FirmwareEdit.All.Select(edit => (edit.Model, edit.Bit)).Distinct().Count());

    [Theory]
    [InlineData(0b1, EditStatus.Installed)]
    [InlineData(0b0, EditStatus.NotInstalled)]
    [InlineData(0b10, EditStatus.NotInstalled)]
    public void TheMarkSaysWhetherTheEditIsOn(int mark, EditStatus status) =>
        Assert.Equal(status, Edit.StatusOn(Build, mark));

    [Fact]
    public void WithNoMarkTheStatusIsUnknown() => Assert.Equal(EditStatus.Unknown, Edit.StatusOn(Build, null));

    [Fact]
    public void BeforeTheFirmwareIsReportedTheStatusIsUnknown() =>
        Assert.Equal(EditStatus.Unknown, Edit.StatusOn(null, 1));

    [Fact]
    public void AnotherFirmwareBuildDoesNotFitWhateverTheMarkSays() =>
        Assert.Equal(EditStatus.DoesNotFit, Edit.StatusOn("4.108.0.0", 1));

    [Fact]
    public void AReadyHeadsetHasNothingInTheWay() => Assert.Empty(Edit.Blockers(Ready()));

    [Fact]
    public void AHeadsetOutOfReachIsTheOnlyThingSaid() =>
        Assert.Equal([EditBlocker.NotConnected], Edit.Blockers(Ready(connected: false, build: null, battery: null, swarm: true)));

    [Fact]
    public void EverythingInTheWayIsSaidAtOnce() =>
        Assert.Equal(
            [EditBlocker.DoesNotFit, EditBlocker.LowBattery, EditBlocker.SwarmRunning],
            Edit.Blockers(Ready(build: "4.108.0.0", battery: 20, swarm: true)));

    [Theory]
    [InlineData(49, true)]
    [InlineData(50, false)]
    public void TheBatteryMustBeAtLeastHalfFull(int battery, bool blocked) =>
        Assert.Equal(blocked, Edit.Blockers(Ready(battery: battery)).Contains(EditBlocker.LowBattery));

    [Fact]
    public void ABatteryNotYetReportedIsTakenAsTooLow() =>
        Assert.Contains(EditBlocker.LowBattery, Edit.Blockers(Ready(battery: null)));

    [Fact]
    public void TheStepsRunCheckBackupWriteReadBackRestartConfirm() =>
        Assert.Equal(
            [EditStep.Check, EditStep.Backup, EditStep.Write, EditStep.ReadBack, EditStep.Restart, EditStep.Confirm],
            FirmwareEdit.Steps);

    [Theory]
    [InlineData(EditStep.Check, false)]
    [InlineData(EditStep.Backup, false)]
    [InlineData(EditStep.Write, true)]
    [InlineData(EditStep.ReadBack, true)]
    [InlineData(EditStep.Restart, true)]
    [InlineData(EditStep.Confirm, true)]
    public void AFailureFromTheWriteOnIsUndone(EditStep failed, bool undo) =>
        Assert.Equal(undo, FirmwareEdit.UndoAfter(failed));
}
