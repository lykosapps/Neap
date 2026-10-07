namespace Neap.Core.Updates;

/// <summary>Decides how a newer version is offered, once one is found.</summary>
/// <remarks>
/// A system that puts the new version in place itself offers it to install.
/// One that does not, such as Linux, or Windows with Neap in a folder it
/// cannot write to, says a version is out and sends the person to the release
/// page, which is <see cref="UpdateStage.CannotUpdateHere"/>.
/// </remarks>
public static class UpdateOffer
{
    /// <param name="canInstall">Whether this system puts a newer version in place of the running one.</param>
    /// <param name="canWriteHere">Whether Neap's own folder can be written to; asked only where installing is possible.</param>
    public static UpdateStage For(bool canInstall, Func<bool> canWriteHere) =>
        canInstall && canWriteHere() ? UpdateStage.Available : UpdateStage.CannotUpdateHere;
}
