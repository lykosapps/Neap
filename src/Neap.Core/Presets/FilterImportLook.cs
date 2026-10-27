namespace Neap.Core.Presets;

/// <summary>What the import dialog says about the filters pasted or opened.</summary>
public enum ImportSaying
{
    NothingYet,
    NothingFits,
    SetsOne,
    SetsSome,
    SetsAll,
}

/// <summary>What the import dialog says for what was read, and whether it can be applied.</summary>
public static class FilterImportLook
{
    /// <summary>The saying for what was read.</summary>
    /// <remarks>
    /// Text with nothing in it that reads as a filter or a preamp is nothing
    /// yet, and says nothing. Text with lines but no filter the headset can
    /// use says so, since applying it would only turn every band off.
    /// </remarks>
    /// <param name="result">What reading the text came to.</param>
    public static ImportSaying Say(FilterImportResult result) => result.Bands.Count switch
    {
        0 when result.NotImported.Count == 0 && result.PreampDb is null => ImportSaying.NothingYet,
        0 => ImportSaying.NothingFits,
        1 => ImportSaying.SetsOne,
        BandWrites.Bands => ImportSaying.SetsAll,
        _ => ImportSaying.SetsSome,
    };

    /// <summary>Whether the bands can be set from what was read: only when at least one filter fits.</summary>
    /// <param name="saying">The saying for what was read.</param>
    public static bool CanApply(ImportSaying saying) => saying is ImportSaying.SetsOne or ImportSaying.SetsSome or ImportSaying.SetsAll;
}
