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
    public void EveryEditHasABitOfItsOwn() =>
        Assert.Equal(FirmwareEdit.All.Count, FirmwareEdit.All.Select(edit => (edit.Model, edit.Bit)).Distinct().Count());

    [Fact]
    public void EverySectionStartsOnASectionBoundary() =>
        Assert.All(FirmwareEdit.All.SelectMany(edit => edit.Sectors), sector => Assert.Equal(0, sector % FirmwareEdit.SectorSize));

    [Fact]
    public void NewCodeIsTheHigherRisk()
    {
        Assert.Equal(EditRisk.Higher, Cycle.Risk);
        Assert.Equal(EditRisk.Low, Light.Risk);
    }

    [Theory]
    [InlineData(0b1, EditStatus.Installed)]
    [InlineData(0b0, EditStatus.NotInstalled)]
    [InlineData(0b10, EditStatus.NotInstalled)]
    public void TheMarkSaysWhetherTheEditIsOn(int mark, EditStatus status) =>
        Assert.Equal(status, Cycle.StatusOn(Build, mark));

    [Fact]
    public void WithNoMarkTheStatusIsUnknown() => Assert.Equal(EditStatus.Unknown, Cycle.StatusOn(Build, null));

    [Fact]
    public void BeforeTheFirmwareIsReportedTheStatusIsUnknown() =>
        Assert.Equal(EditStatus.Unknown, Cycle.StatusOn(null, 1));

    [Fact]
    public void AnotherFirmwareBuildDoesNotFitWhateverTheMarkSays() =>
        Assert.Equal(EditStatus.DoesNotFit, Cycle.StatusOn("4.108.0.0", 1));

    [Fact]
    public void TheMarkListsTheEditsOn() => Assert.Equal(Set(Light), FirmwareEdit.Installed(Edits, 0b10));

    [Fact]
    public void WithNoMarkWhatIsOnIsNotKnown() => Assert.Null(FirmwareEdit.Installed(Edits, null));

    [Fact]
    public void TheMarkOfTheWantedEditsHasTheirBits() => Assert.Equal(0b11, FirmwareEdit.MarkOf([Cycle, Light]));

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
