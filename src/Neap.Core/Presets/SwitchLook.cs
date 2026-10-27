namespace Neap.Core.Presets;

/// <summary>What the screen says about how turning parametric on, or restoring the stock equaliser, went.</summary>
public enum SwitchSaying
{
    TurnedOn,
    RestoredStock,
    WasAlreadyOn,
    WasAlreadyStock,
    CouldNotRead,
    NeedsCable,
    ReadOnly,
    NotChangedAndPutBack,
    NotChangedAndNotPutBack,
}

/// <summary>What the Firmware page's parametric card says for how a change ended.</summary>
public static class SwitchLook
{
    /// <summary>The saying for a result.</summary>
    /// <remarks>
    /// A failed change says whether the headset's earlier settings were put
    /// back, since that is what a person needs to know next: whether to press
    /// Restore.
    /// </remarks>
    /// <param name="result">How the change went.</param>
    /// <param name="turningOn">True for turning parametric on, false for restoring the stock equaliser.</param>
    public static SwitchSaying Say(SwitchResult result, bool turningOn) => result.Outcome switch
    {
        SwitchOutcome.Done => turningOn ? SwitchSaying.TurnedOn : SwitchSaying.RestoredStock,
        SwitchOutcome.AlreadyDone => turningOn ? SwitchSaying.WasAlreadyOn : SwitchSaying.WasAlreadyStock,
        SwitchOutcome.CouldNotRead => SwitchSaying.CouldNotRead,
        SwitchOutcome.NeedsCable => SwitchSaying.NeedsCable,
        SwitchOutcome.ReadOnly => SwitchSaying.ReadOnly,
        _ => result.BackupRestored ? SwitchSaying.NotChangedAndPutBack : SwitchSaying.NotChangedAndNotPutBack,
    };
}
