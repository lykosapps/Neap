using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class EditHistoryTests
{
    private static readonly DateTime T0 = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);

    private static EqualiserSnapshot Bands(int first, int? preset = null) =>
        new([first, 0, 0, 0, 0, 0, 0, 0, 0, 0], null, preset);

    [Fact]
    public void AFreshHistoryHasNothingToUndoOrRedo()
    {
        var history = new EditHistory();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Null(history.Undo());
        Assert.Null(history.Redo());
    }

    [Fact]
    public void UndoPutsBackTheCurveBeforeTheEditAndRedoTheOneAfter()
    {
        var history = new EditHistory();
        history.Record(Bands(0), Bands(30), T0);

        Assert.True(Bands(0).Matches(history.Undo()!));
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.True(Bands(30).Matches(history.Redo()!));
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void StepsComeBackNewestFirst()
    {
        var history = new EditHistory();
        history.Record(Bands(0), Bands(10), T0);
        history.Record(Bands(10), Bands(20), T0.AddSeconds(5));

        Assert.True(Bands(10).Matches(history.Undo()!));
        Assert.True(Bands(0).Matches(history.Undo()!));
    }

    [Fact]
    public void ANewEditAfterAnUndoDropsWhatCouldHaveBeenRedone()
    {
        var history = new EditHistory();
        history.Record(Bands(0), Bands(10), T0);
        history.Undo();

        history.Record(Bands(0), Bands(25), T0.AddSeconds(5));

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void AnEditThatChangesNothingIsNotAStep()
    {
        var history = new EditHistory();

        history.Record(Bands(10), Bands(10), T0);

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void EditsOfOneBurstInQuickSuccessionAreOneStep()
    {
        var history = new EditHistory();
        history.Record(Bands(0), Bands(5), T0, "gain");
        history.Record(Bands(5), Bands(10), T0.AddMilliseconds(200), "gain");
        history.Record(Bands(10), Bands(15), T0.AddMilliseconds(400), "gain");

        Assert.True(Bands(0).Matches(history.Undo()!));
        Assert.False(history.CanUndo);
        Assert.True(Bands(15).Matches(history.Redo()!));
    }

    [Fact]
    public void APauseOrAnotherBurstStartsANewStep()
    {
        var history = new EditHistory();
        history.Record(Bands(0), Bands(5), T0, "gain");
        history.Record(Bands(5), Bands(10), T0.AddSeconds(2), "gain");
        history.Record(Bands(10), Bands(15), T0.AddSeconds(2.1), "width");

        Assert.True(Bands(10).Matches(history.Undo()!));
        Assert.True(Bands(5).Matches(history.Undo()!));
        Assert.True(Bands(0).Matches(history.Undo()!));
    }

    [Fact]
    public void ABurstThatEndsWhereItBeganLeavesNoStep()
    {
        var history = new EditHistory();
        history.Record(Bands(0), Bands(5), T0, "gain");
        history.Record(Bands(5), Bands(0), T0.AddMilliseconds(100), "gain");

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void ThePresetChosenIsPartOfWhatIsPutBack()
    {
        var history = new EditHistory();
        history.Record(Bands(0, preset: 2), Bands(0, preset: 3), T0);

        Assert.Equal(2, history.Undo()!.BaselineId);
        Assert.Equal(3, history.Redo()!.BaselineId);
    }

    [Fact]
    public void AdjustmentsAreComparedAsWellAsBands()
    {
        var a = new EqualiserSnapshot(new int[10], [new Adjustment(1000, 30, 10)], null);
        var b = new EqualiserSnapshot(new int[10], [new Adjustment(1000, 40, 10)], null);
        var history = new EditHistory();

        history.Record(a, b, T0);

        Assert.True(history.CanUndo);
        Assert.True(a.Matches(history.Undo()!));
    }

    [Fact]
    public void ClearingForgetsEverything()
    {
        var history = new EditHistory();
        history.Record(Bands(0), Bands(10), T0);
        history.Record(Bands(10), Bands(20), T0.AddSeconds(3));
        history.Undo();

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void OnlyTheLatestStepsAreKept()
    {
        var history = new EditHistory();
        for (int i = 0; i < EditHistory.Most + 20; i++)
            history.Record(Bands(i * 5), Bands(i * 5 + 5), T0.AddSeconds(i));

        int undone = 0;
        while (history.Undo() is not null) undone++;

        Assert.Equal(EditHistory.Most, undone);
    }
}
