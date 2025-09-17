using System;
using System.IO;
using System.Linq;
using Xunit;

namespace CollectSFData.Tests
{
    [Collection("TestAuditCollection")] // ensure fixture has run
    public class AuditFixtureTests
    {
        [Fact]
        public void AuditLatestJsonExists()
        {
            // Discover repo root similarly to fixture logic (climb for .git or solution)
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            DirectoryInfo? solutionRoot = null;
            DirectoryInfo? repoRoot = null;
            for (int i = 0; i < 12 && dir != null; i++)
            {
                if (solutionRoot == null && dir.GetFiles("*.sln").Any())
                {
                    solutionRoot = dir;
                }
                if (dir.GetDirectories(".git").Any())
                {
                    repoRoot = dir;
                    break;
                }
                dir = dir.Parent;
            }
            repoRoot ??= solutionRoot ?? new DirectoryInfo(AppContext.BaseDirectory);
            var auditsDir = Path.Combine(repoRoot.FullName, "test-audits");

            // The fixture writes latest.json during construction (phase = start), so it should exist now.
            var latestPath = Path.Combine(auditsDir, "latest.json");
            Assert.True(File.Exists(latestPath), $"Expected audit latest.json at {latestPath} but it was not found.");

            // Basic sanity: file should have non-trivial content
            var length = new FileInfo(latestPath).Length;
            Assert.True(length > 50, $"latest.json seems unexpectedly small ({length} bytes).");
        }
    }
}
