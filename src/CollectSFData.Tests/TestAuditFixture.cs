using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

// Assembly-level fixture to emit a lightweight audit metadata file once per test run.
public class TestAuditFixture : IDisposable
{
    private string? _resultsDir;
    private string? _auditFile;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly string _runId = Guid.NewGuid().ToString("N");
    private string? _repoRoot; // folder containing .git (preferred) else solution root
    private string? _trxPath;

    public TestAuditFixture()
    {
        // First locate a solution; keep climbing further to prefer a folder containing .git.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        DirectoryInfo? solutionRoot = null;
        for (int i = 0; i < 12 && dir != null; i++)
        {
            if (solutionRoot == null && dir.GetFiles("*.sln").Any())
            {
                solutionRoot = dir;
            }
            if (dir.GetDirectories(".git").Any())
            {
                _repoRoot = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }
        _repoRoot ??= solutionRoot?.FullName ?? new DirectoryInfo(AppContext.BaseDirectory).FullName;
        _resultsDir = Path.Combine(_repoRoot, "test-audits");
        try { Directory.CreateDirectory(_resultsDir); } catch { }
        _auditFile = Path.Combine(_resultsDir, $"audit_metadata_{_runId}.json");
        WriteMetadata("start");
    }

    // Removed probing logic; relying on VSTEST_RESULTS_DIRECTORY or local fallback.

    private void WriteMetadata(string phase)
    {
        try
        {
            var testAssembly = Assembly.GetExecutingAssembly();
            var tfms = testAssembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(a => a.Key == "TargetFramework")
                .Select(a => a.Value)
                .ToList();

            var git = GetGitMetadata();
            object? coverageSummary = null;
            object? trxSummary = null;
            object? failedTests = null;
            if (phase == "end")
            {
                coverageSummary = AggregateCoverage();
                (trxSummary, failedTests) = ParseTrx();
            }

            var payload = new
            {
                phase,
                runId = _runId,
                timestampUtc = DateTime.UtcNow,
                durationMs = _sw.ElapsedMilliseconds,
                machine = Environment.MachineName,
                osDescription = RuntimeInformation.OSDescription,
                osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                frameworkDescription = RuntimeInformation.FrameworkDescription,
                targetFrameworks = tfms,
                git,
                environment = new
                {
                    username = Environment.UserName,
                    ci = Environment.GetEnvironmentVariable("CI") ?? "false",
                    buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID"),
                    buildDefinition = Environment.GetEnvironmentVariable("BUILD_DEFINITIONNAME")
                },
                assembly = new
                {
                    name = testAssembly.GetName().Name,
                    version = testAssembly.GetName().Version?.ToString(),
                    location = testAssembly.Location
                },
                coverage = coverageSummary,
                tests = trxSummary,
                failedTests
            };

            if (_auditFile != null)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(payload, options);
                File.AppendAllText(_auditFile, json + Environment.NewLine);
                // Update consolidated latest.json for easy consumption
                var latestPath = Path.Combine(Path.GetDirectoryName(_auditFile)!, "latest.json");
                File.WriteAllText(latestPath, json);
            }
        }
        catch
        {
            // best-effort; never fail test run for audit file issues
        }
    }

    public void Dispose()
    {
        _sw.Stop();
        WriteMetadata("end");
    }

    private object? GetGitMetadata()
    {
        try
        {
            if (_repoRoot == null) return null;
            var headPath = Path.Combine(_repoRoot, ".git", "HEAD");
            if (!File.Exists(headPath)) return null;
            var head = File.ReadAllText(headPath).Trim();
            string? commit = null;
            string? branch = null;
            if (head.StartsWith("ref:"))
            {
                var refName = head.Substring(5).Trim();
                branch = refName.Replace("refs/heads/", "");
                var refPath = Path.Combine(_repoRoot, ".git", refName.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(refPath)) commit = File.ReadAllText(refPath).Trim();
            }
            else
            {
                commit = head; // detached HEAD
            }
            return new { branch, commit };
        }
        catch { return null; }
    }

    private object? AggregateCoverage()
    {
        try
        {
            if (_repoRoot == null) return null;
            var openCoverFiles = Directory.GetFiles(_repoRoot, "coverage.opencover.xml", SearchOption.AllDirectories);
            long totalVisited = 0;
            long totalLines = 0;
            foreach (var file in openCoverFiles)
            {
                try
                {
                    var doc = XDocument.Load(file);
                    // OpenCover summary may be <Summary numSequencePoints= visitedSequencePoints=> OR numLines/visitedLines
                    var summary = doc.Descendants("Summary").FirstOrDefault();
                    if (summary != null)
                    {
                        long visited = TryAttr(summary, "visitedLines") ?? TryAttr(summary, "visitedSequencePoints") ?? 0;
                        long lines = TryAttr(summary, "numLines") ?? TryAttr(summary, "numSequencePoints") ?? 0;
                        totalVisited += visited;
                        totalLines += lines;
                    }
                }
                catch { /* ignore individual file parse errors */ }
            }
            if (totalLines == 0) return null;
            double pct = (double)totalVisited / totalLines * 100;
            return new { visitedLines = totalVisited, totalLines, lineRatePercent = Math.Round(pct, 2), files = openCoverFiles.Length };
        }
        catch { return null; }
    }

    private static long? TryAttr(XElement el, string name)
        => long.TryParse(el.Attribute(name)?.Value, out var v) ? v : null;

    private (object? summary, object? failedTests) ParseTrx()
    {
        try
        {
            if (_repoRoot == null) return (null, null);
            var trxFiles = Directory.GetFiles(_repoRoot, "*.trx", SearchOption.AllDirectories);
            var latest = trxFiles.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault();
            if (latest == null) return (null, null);
            var doc = XDocument.Load(latest);
            var resultSummary = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ResultSummary");
            object? summaryObj = null;
            if (resultSummary != null)
            {
                var counters = resultSummary.Elements().FirstOrDefault(e => e.Name.LocalName == "Counters");
                if (counters != null)
                {
                    int TryInt(string a) => int.TryParse(counters.Attribute(a)?.Value, out var v) ? v : 0;
                    summaryObj = new
                    {
                        total = TryInt("total"),
                        executed = TryInt("executed"),
                        passed = TryInt("passed"),
                        failed = TryInt("failed"),
                        error = TryInt("error"),
                        timeout = TryInt("timeout"),
                        aborted = TryInt("aborted"),
                        inconclusive = TryInt("inconclusive")
                    };
                }
            }
            var failed = doc.Descendants().Where(e => e.Name.LocalName == "UnitTestResult" && e.Attribute("outcome")?.Value != "Passed")
                .Select(e => new
                {
                    testName = e.Attribute("testName")?.Value,
                    outcome = e.Attribute("outcome")?.Value,
                    duration = e.Attribute("duration")?.Value,
                    errorMessage = e.Descendants().FirstOrDefault(x => x.Name.LocalName == "Message")?.Value?.Trim()
                })
                .ToList();
            return (summaryObj, failed.Any() ? failed : null);
        }
        catch { return (null, null); }
    }
}

[CollectionDefinition("TestAuditCollection")]
public class TestAuditCollection : ICollectionFixture<TestAuditFixture>
{
    // Intentionally empty - serves as anchor for the fixture.
}
