using Neap.Core.Audio;

namespace Neap.Core.Tests.Audio;

public class SpatialSoundTests
{
    private static readonly Guid AtmosForSpeakers = new("4C81E564-C8EF-4AD9-9F2C-9EF995533790");

    [Fact]
    public void OffIsAlwaysOfferedFirst() =>
        Assert.Equal([SpatialFormat.Off], SpatialSound.Offered(_ => false));

    [Fact]
    public void OnlyTheSupportedHeadphoneFormatsAreOffered()
    {
        var supported = new[]
        {
            SpatialSound.Subtypes[SpatialFormat.DolbyAtmos],
            SpatialSound.Subtypes[SpatialFormat.WindowsSonic],
            AtmosForSpeakers,
        };

        var offered = SpatialSound.Offered(supported.Contains);

        Assert.Equal([SpatialFormat.Off, SpatialFormat.WindowsSonic, SpatialFormat.DolbyAtmos], offered);
    }

    [Fact]
    public void AnEmptyIdentifierMeansOff() =>
        Assert.Equal(SpatialFormat.Off, SpatialSound.Of(Guid.Empty));

    [Fact]
    public void AKnownIdentifierIsItsFormat() =>
        Assert.Equal(SpatialFormat.DolbyAtmos, SpatialSound.Of(SpatialSound.Subtypes[SpatialFormat.DolbyAtmos]));

    [Fact]
    public void AFormatNotOfferedIsNotMistakenForOne() =>
        Assert.Null(SpatialSound.Of(AtmosForSpeakers));

    [Fact]
    public void OffIsAskedForWithAnEmptyIdentifier() =>
        Assert.Equal(Guid.Empty, SpatialSound.SubtypeOf(SpatialFormat.Off));

    [Theory]
    [InlineData(SpatialResult.Succeeded, SpatialNote.None)]
    [InlineData(SpatialResult.LicenseExpired, SpatialNote.NeedsLicence)]
    [InlineData(SpatialResult.LicenseNotValidForAudioEndpoint, SpatialNote.NeedsLicence)]
    [InlineData(SpatialResult.AccessDenied, SpatialNote.Refused)]
    [InlineData(SpatialResult.NotSupportedOnAudioEndpoint, SpatialNote.Refused)]
    [InlineData(SpatialResult.UnknownError, SpatialNote.Refused)]
    public void EachResultHasItsNote(SpatialResult result, SpatialNote note) =>
        Assert.Equal(note, SpatialSound.NoteFor(result));
}
