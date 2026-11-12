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

    // SHA-256 of the code as SP2Tools assembled and checked it, so a mistyped byte is caught here and not on a headset.
    [Fact]
    public void TheNoiseCyclesCodeIsByteForByteWhatSp2ToolsBuilt()
    {
        var code = Cycle.Changes[0];
        Assert.Equal(158, code.Active.Count);
        Assert.Equal(code.Active.Count, code.Factory.Count);
        Assert.Equal("005202A9DB97FF819E15CBCDE7C5C40BAB82D1E3789E7CEF9A7BD3554B1E0553",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData([.. code.Active])));
    }

    [Fact]
    public void AHeadsetWithTheNoiseCyclesOlderCodeReadsAsChangedAndIsWrittenOver()
    {
        // The code before it sent the level: shorter, with empty flash after it.
        byte[] older = Convert.FromHexString(
            "70B582B000240094019475206946E8F63DF840B3762001A9E8F638F818B3009B53B1019B322B03D264240125092606E0FF240025FF26" +
            "02E000240125FF26FF2C04D00194762001A9E8F6D0FD009575206946E4F69DFFFF2E02D03046DBF67CFA012002B070BD002002B070BD");
        var code = Cycle.Changes[0];
        byte[] held = [.. older, .. Enumerable.Repeat((byte)0xFF, code.Active.Count - older.Length)];
        var flash = new Dictionary<int, byte[]> { [code.Address] = held, [Cycle.Changes[1].Address] = [.. Cycle.Changes[1].Active] };
        Assert.Equal(EditStatus.Altered, Cycle.StatusIn(Build, flash));

        byte[] section = [.. held, .. Enumerable.Repeat((byte)0xFF, FirmwareEdit.SectorSize - held.Length)];
        byte[]? written = FirmwareEdit.Compose(code.Address, section, Set(Cycle), FirmwareEdit.FactorySections);
        Assert.NotNull(written);
        Assert.Equal(code.Active, written[..code.Active.Count]);
    }

    [Fact]
    public void TheAtlasAirHasNoEdits() => Assert.Empty(FirmwareEdit.For(HeadsetModels.AtlasAir));

    [Fact]
    public void AnUnknownHeadsetHasNoEdits() => Assert.Empty(FirmwareEdit.For(null));

    [Fact]
    public void EditsAreOfferedForTheStealthProIIAlone()
    {
        Assert.Equal(OperatingSystem.IsWindows(), FirmwareEdit.Offered(HeadsetModels.StealthProII));
        Assert.False(FirmwareEdit.Offered(HeadsetModels.AtlasAir));
        Assert.False(FirmwareEdit.Offered(null));
    }

    [Fact]
    public void EverySectionStartsOnASectionBoundary() =>
        Assert.All(FirmwareEdit.All.SelectMany(edit => edit.Sectors), sector => Assert.Equal(0, sector % FirmwareEdit.SectorSize));

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
        Assert.Equal(WithCode(), FirmwareEdit.Compose(CodeSector, Empty(), Set(Cycle), FirmwareEdit.FactorySections));

    [Fact]
    public void UntickingAnEditPutsTheFactoryBytesBack() =>
        Assert.Equal(Empty(), FirmwareEdit.Compose(CodeSector, WithCode(), Set(), FirmwareEdit.FactorySections));

    [Fact]
    public void ASectionHoldingSomethingUnknownIsNotWritten()
    {
        byte[] held = Empty();
        held[0x800] = 0;
        Assert.Null(FirmwareEdit.Compose(CodeSector, held, Set(Cycle), FirmwareEdit.FactorySections));
    }

    [Fact]
    public void APartSectionIsNotWritten() => Assert.Null(FirmwareEdit.Compose(CodeSector, Empty().AsSpan(0, 16), Set(Cycle), FirmwareEdit.FactorySections));

    [Fact]
    public void ASectionNoEditTouchesIsNotWritten() => Assert.Null(FirmwareEdit.Compose(0x08100000, Empty(), Set(Cycle), FirmwareEdit.FactorySections));

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
            [EditStep.Check, EditStep.Plug, EditStep.Connect, EditStep.Backup, EditStep.Write],
            FirmwareEdit.Steps);

    [Theory]
    [InlineData("Turtle Beach Swarm II", true)]
    [InlineData("Turtle Beach Device Service", true)]
    [InlineData("turtle beach swarm ii updater", true)]
    [InlineData("Neap.Desktop", false)]
    [InlineData("Discord", false)]
    public void AnyOfSwarmIIsProgramsCountsAsSwarmRunning(string program, bool swarm) =>
        Assert.Equal(swarm, FirmwareEdit.SwarmIn(["explorer", program]));

    [Theory]
    [InlineData(true, new[] { EditStatus.Installed, EditStatus.Unknown }, true)]
    [InlineData(true, new[] { EditStatus.Installed, EditStatus.NotInstalled }, false)]
    [InlineData(true, new[] { EditStatus.DoesNotFit, EditStatus.Altered }, false)]
    [InlineData(false, new[] { EditStatus.Unknown, EditStatus.Unknown }, false)]
    public void CheckAgainIsOfferedOnlyForAnUnknownStateOnAConnectedHeadset(bool connected, EditStatus[] statuses, bool offered) =>
        Assert.Equal(offered, FirmwareEdit.OffersCheckAgain(statuses, connected));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(30, 45)]
    [InlineData(60, 90)]
    [InlineData(150, 90)]
    public void TheWriteBarKeepsTimeWithTheUsualMinuteAndStopsShortOfFull(int seconds, int shown) =>
        Assert.Equal(shown, FirmwareEdit.WriteShown(TimeSpan.FromSeconds(seconds)));

    // -- each edit's state from the sections read back ------------------------

    private static byte[] Section(int sector, params FlashChange[] active)
    {
        var bytes = new byte[FirmwareEdit.SectorSize];
        foreach (var change in Edits.SelectMany(edit => edit.Changes).Where(change => (change.Address & ~(FirmwareEdit.SectorSize - 1)) == sector))
            (active.Contains(change) ? change.Active : change.Factory).ToArray().CopyTo(bytes, change.Address - sector);
        return bytes;
    }

    private static int SectorOf(FlashChange change) => change.Address & ~(FirmwareEdit.SectorSize - 1);

    [Fact]
    public void ASectionReadBackGivesTheStateOfTheEditInIt()
    {
        var light = Light.Changes[0];
        var flash = FirmwareEdit.FlashFrom(Edits, new Dictionary<int, byte[]> { [SectorOf(light)] = Section(SectorOf(light), light) }, null);
        Assert.Equal(EditStatus.Installed, Light.StatusIn(Build, flash));
        Assert.Equal(EditStatus.Unknown, Cycle.StatusIn(Build, flash));
    }

    [Fact]
    public void ASectionReadBackReplacesWhatWasKnownOfItAndKeepsTheRest()
    {
        var light = Light.Changes[0];
        var before = Flash((CycleCode, true), (CycleCall, true), (light, true));
        var flash = FirmwareEdit.FlashFrom(Edits, new Dictionary<int, byte[]> { [SectorOf(light)] = Section(SectorOf(light)) }, before);
        Assert.Equal(EditStatus.NotInstalled, Light.StatusIn(Build, flash));
        Assert.Equal(EditStatus.Installed, Cycle.StatusIn(Build, flash));
    }

    [Fact]
    public void EverySectionReadBackGivesEveryEditsState()
    {
        var sections = Edits.SelectMany(edit => edit.Sectors).Distinct()
            .ToDictionary(sector => sector, sector => Section(sector, CycleCode, CycleCall));
        var flash = FirmwareEdit.FlashFrom(Edits, sections, null);
        Assert.Equal(Set(Cycle), FirmwareEdit.InstalledIn(Edits, Build, flash));
    }

    // -- a read back is believed only after a differing word is read again -----

    private static readonly byte[] Written = [.. Enumerable.Range(0, 64).Select(i => (byte)(i + 1))];

    private static byte[] WithWord(byte[] bytes, int word, params byte[] value)
    {
        var copy = bytes.ToArray();
        value.CopyTo(copy, word * FlashRead.WordSize);
        return copy;
    }

    [Fact]
    public void AReadBackThatMatchesIsNotReadAgain()
    {
        int reads = 0;
        Assert.Null(FlashRead.FirstDifference(0x1000, Written, Written, _ => { reads++; return null; }));
        Assert.Equal(0, reads);
    }

    [Fact]
    public void AWordWithItsLastBytesZeroedThatReadsRightAgainIsNoDifference()
    {
        // As seen over the cable on 3 November 2026: 5F494E20 read once as 5F490000.
        var read = WithWord(Written, 3, Written[12], Written[13], 0, 0);
        Assert.Null(FlashRead.FirstDifference(0x1000, Written, read, at => Written[(at - 0x1000)..(at - 0x1000 + 4)]));
    }

    [Fact]
    public void AWordThatNeverReadsAsWrittenIsTheDifference()
    {
        var read = WithWord(Written, 5, 9, 9, 9, 9);
        Assert.Equal(0x1000 + 5 * FlashRead.WordSize,
            FlashRead.FirstDifference(0x1000, Written, read, _ => [9, 9, 9, 9]));
    }

    [Fact]
    public void AWordThatGivesNoReplyWhenReadAgainIsTheDifference() =>
        Assert.Equal(0x1000, FlashRead.FirstDifference(0x1000, Written, WithWord(Written, 0, 0, 0, 0, 0), _ => null));

    [Fact]
    public void NoSectionReadAndNothingKnownBeforeIsNothingKnown() =>
        Assert.Null(FirmwareEdit.FlashFrom(Edits, new Dictionary<int, byte[]>(), null));
}
