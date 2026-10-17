using Neap.Core.Diagnostics;
using Neap.Core.Tests.Resources;

namespace Neap.Core.Tests.Diagnostics;

public class SupportMatrixTests
{
    private static string DocPath =>
        Path.Combine(AppSource.RepoRoot(), "docs", "SUPPORT-MATRIX.md");

    /// <summary>
    /// The committed matrix matches the one built from the models. Set
    /// <c>UPDATE_GOLDEN=1</c> to rewrite the file after changing the models.
    /// </summary>
    [Fact]
    public void TheDocMatchesTheModels()
    {
        string generated = SupportMatrix.Markdown();
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(DocPath, generated);
            return;
        }
        Assert.Equal(generated.ReplaceLineEndings(), File.ReadAllText(DocPath).ReplaceLineEndings());
    }
}
