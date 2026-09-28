namespace MediFlow.BuildingBlocks.Common.Tests;

/// <summary>
/// TEMPORARY - DO NOT MERGE.
///
/// Deliberately failing test used to verify that the branch ruleset actually
/// blocks a merge when CI is red. A guardrail that has never been observed
/// firing is not a guardrail; it is an assumption.
///
/// Expected outcome on the pull request:
///   1. The "Build &amp; Test" check turns red.
///   2. The merge button is locked with "Required status check ... expected".
///   3. This branch is closed without merging and then deleted.
/// </summary>
public class CiGateProbeTests
{
    [Fact]
    public void DeliberatelyFailingTest_Should_BlockTheMerge()
    {
        // Arrange
        Result result = Result.Success();

        // Act + Assert - this assertion is false on purpose.
        Assert.True(
            result.IsFailure,
            "Deliberate failure: proving the CI gate blocks merging. Never merge this branch.");
    }
}
