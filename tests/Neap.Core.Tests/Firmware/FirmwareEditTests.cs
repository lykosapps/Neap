using Neap.Core.Firmware;

namespace Neap.Core.Tests.Firmware;

public class FirmwareEditTests
{
    private static readonly FirmwareEdit Cycle = FirmwareEdit.NoiseCycle;
    private static readonly FirmwareEdit Light = FirmwareEdit.LightOffWhenDisconnected;
    private static readonly IReadOnlyList<FirmwareEdit> Edits = FirmwareEdit.For(HeadsetModels.StealthProII);
    private const string Build = "4.107.703.0";

    private static HashSet<FirmwareEdit> Set(params FirmwareEdit[] edits) => [.. edits];

    private static EditReadiness Ready(
        bool connected = true, string? build = Build, int? battery = 80, bool swarm = false) =>
        new(connected, build, battery, swarm);

    // -- which edits, and whether they are on ----------------------------------

    [Fact]
    public void TheStealthProIIHasTheNoiseCycleAndTheLight() => Assert.Equal([Cycle, Light], Edits);

    [Fact]
    public void TheAtlasAirHasNoEdits() => Assert.Empty(FirmwareEdit.For(HeadsetModels.AtlasAir));

    [Fact]
    public void AnUnknownHeadsetHasNoEdits() => Assert.Empty(FirmwareEdit.For(null));

    [Fact]
    public void EverySectionStartsOnASectionBoundary() =>
        Assert.All(FirmwareEdit.All.SelectMany(edit => edit.Sectors), sector => Assert.Equal(0, sector % FirmwareEdit.SectorSize));

    [Fact]
    public void NewCodeIsTheHigherRisk()
    {
        Assert.Equal(EditRisk.Higher, Cycle.Risk);
        Assert.Equal(EditRisk.Low, Light.Risk);
    }

    // -- read live from the flash ---------------------------------------------

    private static Dictionary<int, byte[]> Flash(params (FlashChange Change, bool Active)[] read) =>
        read.ToDictionary(r => r.Change.Address, r => (r.Active ? r.Change.Active : r.Change.Factory).ToArray());

    private static FlashChange CycleCode => Cycle.Changes[0];
    private static FlashChange CycleCall => Cycle.Changes[1];

    [Fact]
    public void EveryChangeLiesInASectionItsEditListsAndKeepsItsLength() =>
        Assert.All(FirmwareEdit.All.SelectMany(edit => edit.Changes.Select(change => (edit, change))), pair =>
        {
            Assert.Contains(pair.change.Address & ~(FirmwareEdit.SectorSize - 1), pair.edit.Sectors);
            Assert.Equal(pair.change.Factory.Count, pair.change.Active.Count);
        });

    [Fact]
    public void FactoryBytesEverywhereIsOff() =>
        Assert.Equal(EditStatus.NotInstalled, Cycle.StatusIn(Build, Flash((CycleCode, false), (CycleCall, false))));

    [Fact]
    public void TheEditsBytesEverywhereIsOn() =>
        Assert.Equal(EditStatus.Installed, Cycle.StatusIn(Build, Flash((CycleCode, true), (CycleCall, true))));

    [Fact]
    public void NewCodeThatNothingCallsIsOff() =>
        Assert.Equal(EditStatus.NotInstalled, Cycle.StatusIn(Build, Flash((CycleCode, true), (CycleCall, false))));

    [Fact]
    public void ACallToCodeThatIsNotThereIsAltered() =>
        Assert.Equal(EditStatus.Altered, Cycle.StatusIn(Build, Flash((CycleCode, false), (CycleCall, true))));

    [Fact]
    public void BytesThatAreNeitherAreAltered()
    {
        var flash = Flash((CycleCode, true), (CycleCall, true));
        flash[CycleCall.Address] = [1, 2, 3, 4];
        Assert.Equal(EditStatus.Altered, Cycle.StatusIn(Build, flash));
    }

    [Fact]
    public void APlaceNotReadLeavesTheStatusUnknown() =>
        Assert.Equal(EditStatus.Unknown, Cycle.StatusIn(Build, Flash((CycleCode, true))));

    [Fact]
    public void FlashThatCouldNotBeReadLeavesTheStatusUnknown() =>
        Assert.Equal(EditStatus.Unknown, Cycle.StatusIn(Build, null));

    [Fact]
    public void AnotherBuildDoesNotFitWhateverTheFlashHolds() =>
        Assert.Equal(EditStatus.DoesNotFit, Cycle.StatusIn("4.108.0.0", Flash((CycleCode, true), (CycleCall, true))));

    [Fact]
    public void TheFlashListsTheEditsOn() =>
        Assert.Equal(Set(Light), FirmwareEdit.InstalledIn(Edits, Build,
            Flash((CycleCode, false), (CycleCall, false), (Light.Changes[0], true))));

    [Fact]
    public void AnAlteredEditLeavesWhatIsOnUnknown() =>
        Assert.Null(FirmwareEdit.InstalledIn(Edits, Build,
            Flash((CycleCode, false), (CycleCall, true), (Light.Changes[0], true))));

    // -- what a section is written as -------------------------------------------

    private const int CodeSector = 0x08349000;

    private static byte[] Empty() => Enumerable.Repeat((byte)0xFF, FirmwareEdit.SectorSize).ToArray();

    private static byte[] WithCode()
    {
        byte[] section = Empty();
        CycleCode.Active.ToArray().CopyTo(section, 0);
        return section;
    }

    [Fact]
    public void TickingAnEditPutsItsBytesInAndLeavesTheRestAsHeld() =>
        Assert.Equal(WithCode(), FirmwareEdit.Compose(CodeSector, Empty(), Set(Cycle)));

    [Fact]
    public void UntickingAnEditPutsTheFactoryBytesBack() =>
        Assert.Equal(Empty(), FirmwareEdit.Compose(CodeSector, WithCode(), Set()));

    [Fact]
    public void ASectionHoldingSomethingUnknownIsNotWritten()
    {
        byte[] held = Empty();
        held[0x800] = 0;
        Assert.Null(FirmwareEdit.Compose(CodeSector, held, Set(Cycle)));
    }

    [Fact]
    public void APartSectionIsNotWritten() => Assert.Null(FirmwareEdit.Compose(CodeSector, Empty().AsSpan(0, 16), Set(Cycle)));

    [Fact]
    public void ASectionNoEditTouchesIsNotWritten() => Assert.Null(FirmwareEdit.Compose(0x08100000, Empty(), Set(Cycle)));

    // -- the plan ---------------------------------------------------------------

    [Fact]
    public void WantingWhatIsOnWritesNothing() =>
        Assert.True(FirmwareEdit.Plan(Edits, Set(Cycle), Set(Cycle)).Nothing);

    [Fact]
    public void TickingAnEditWritesOnlyItsSections()
    {
        var plan = FirmwareEdit.Plan(Edits, Set(Cycle, Light), Set(Cycle));
        Assert.Equal([Light], plan.Changing);
        Assert.Equal([0x082F9000], plan.Sectors);
        Assert.Equal(1, plan.Sessions);
    }

    [Fact]
    public void TwoSectionsGoInOneSession()
    {
        var plan = FirmwareEdit.Plan(Edits, Set(Cycle), Set());
        Assert.Equal(2, plan.Sectors.Count);
        Assert.Equal(1, plan.Sessions);
    }

    [Fact]
    public void PuttingTheCycleOnWritesItsCodeBeforeTheCallThatReachesIt() =>
        Assert.Equal([0x08349000, 0x08232000], FirmwareEdit.Plan(Edits, Set(Cycle), Set()).Sectors);

    [Fact]
    public void TakingTheCycleOffPutsTheCallBackBeforeItsCode() =>
        Assert.Equal([0x08232000, 0x08349000], FirmwareEdit.Plan(Edits, Set(), Set(Cycle)).Sectors);

    [Fact]
    public void EditsComingOffAreWrittenBeforeEditsGoingOn() =>
        Assert.Equal([0x082F9000, 0x08349000, 0x08232000], FirmwareEdit.Plan(Edits, Set(Cycle), Set(Light)).Sectors);

    [Fact]
    public void ThreeSectionsTakeTwoSessions()
    {
        var plan = FirmwareEdit.Plan(Edits, Set(Cycle, Light), Set());
        Assert.Equal(3, plan.Sectors.Count);
        Assert.Equal(2, plan.Sessions);
        Assert.Equal([[0x08349000, 0x08232000], [0x082F9000]], FirmwareEdit.Sessions(plan));
    }

    [Fact]
    public void UntickingEverythingRestoresStock()
    {
        var plan = FirmwareEdit.Plan(Edits, Set(), Set(Cycle, Light));
        Assert.Equal([Cycle, Light], plan.Changing);
        Assert.Equal(3, plan.Sectors.Count);
    }

    [Fact]
    public void WithWhatIsOnUnknownEverySectionIsReadAndOnlyWhatDiffersWritten()
    {
        var plan = FirmwareEdit.Plan(Edits, Set(Light), installed: null);
        Assert.True(plan.UpToSectors);
        Assert.Equal(3, plan.Sectors.Count);
    }

    [Fact]
    public void ASectionHoldsOnlyTheTickedEditsInIt()
    {
        Assert.Equal([Light], FirmwareEdit.In(0x082F9000, Set(Cycle, Light)));
        Assert.Empty(FirmwareEdit.In(0x082F9000, Set(Cycle)));
    }

    // -- before writing ---------------------------------------------------------

    [Fact]
    public void AReadyHeadsetHasNothingInTheWay() => Assert.Empty(FirmwareEdit.Blockers([Cycle], Ready()));

    [Fact]
    public void AHeadsetOutOfReachIsTheOnlyThingSaid() =>
        Assert.Equal([EditBlocker.NotConnected], FirmwareEdit.Blockers([Cycle], Ready(connected: false, build: null, battery: null, swarm: true)));

    [Fact]
    public void EverythingInTheWayIsSaidAtOnce() =>
        Assert.Equal(
            [EditBlocker.DoesNotFit, EditBlocker.LowBattery, EditBlocker.SwarmRunning],
            FirmwareEdit.Blockers([Cycle], Ready(build: "4.108.0.0", battery: 20, swarm: true)));

    [Theory]
    [InlineData(49, true)]
    [InlineData(50, false)]
    public void TheBatteryMustBeAtLeastHalfFull(int battery, bool blocked) =>
        Assert.Equal(blocked, FirmwareEdit.Blockers([Cycle], Ready(battery: battery)).Contains(EditBlocker.LowBattery));

    [Fact]
    public void ABatteryNotYetReportedIsTakenAsTooLow() =>
        Assert.Contains(EditBlocker.LowBattery, FirmwareEdit.Blockers([Cycle], Ready(battery: null)));

    [Fact]
    public void TheStepsRunAsTheSpecSetsThemOut() =>
        Assert.Equal(
            [EditStep.Check, EditStep.Plug, EditStep.Connect, EditStep.Backup, EditStep.Write,
             EditStep.Verify, EditStep.Reconnect, EditStep.Confirm],
            FirmwareEdit.Steps);
}
