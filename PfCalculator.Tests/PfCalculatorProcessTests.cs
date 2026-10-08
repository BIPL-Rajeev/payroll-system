using System.Diagnostics;
using Xunit;

namespace PfCalculator.Tests;

// Integration tests for the interactive console loop in PfCalculator (top-level
// Program.cs). Each test runs the actual built app as a child process with
// piped stdin, so the EOF guard, re-prompt behaviour and arithmetic are all
// exercised exactly as a user would run the calculator.
//
// The app is run through `dotnet <built-dll>` directly, pointing at the binary
// produced by the project reference (PfCalculator/bin/<config>/net10.0/).
public class PfCalculatorProcessTests
{
    // The project reference builds the app, but the tests need to know whether
    // the app binary lives under Debug or Release: `dotnet test` defaults to
    // Debug (Release is opt-in with -c), so mirror that.
    private static string Configuration =>
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";

    [Fact]
    public async Task EmptyPipedInput_ExitsWithCode1_AndPrintsAbortMessage()
    {
        var (code, stdout, stderr) = await RunAsync("\n"); // echo "" | dotnet run

        Assert.Equal(1, code);
        Assert.Contains("No more input. Aborting.", stderr);

    }

    [Fact]
    public async Task ImmediateEof_ExitsWithCode1_AndPrintsAbortMessage()
    {
        var (code, _, stderr) = await RunAsync(""); // no bytes at all

        Assert.Equal(1, code);
        Assert.Contains("No more input. Aborting.", stderr);
    }

    [Fact]
    public async Task ValidInput_RemainsUnchanged()
    {
        // Floor ceiling -> no ceiling applied; basic 50,000 x 12% = 6,000.00.
        var (code, stdout, _) = await RunAsync("0\n50000\n");

        Assert.Equal(0, code);
        Assert.Contains("Employee EPF (12%)    : 6,000.00", stdout);
        Assert.Contains("Basic Salary          : 50,000.00", stdout);
        Assert.DoesNotContain("Invalid input", stdout);
    }

    [Fact]
    public async Task InvalidInput_RemainsUnchanged_RePrompts()
    {
        // "abc" and "-5" are rejected at the ceiling prompt; "0" (no ceiling) and
        // "50000" then compute the same result as ValidInput.
        var (code, stdout, stderr) = await RunAsync("abc\n-5\n0\n50000\n");

        Assert.Equal(0, code);
        Assert.Contains("Invalid input. Please enter a non-negative number.", stdout);
        Assert.Contains("Employee EPF (12%)    : 6,000.00", stdout);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string stdin)
    {
        var appPath = Path.Combine(
            FindRepoRoot(),
            "bin",
            Configuration,
            "net10.0",
            "PfCalculator.dll");

        var start = new ProcessStartInfo
        {
            // Run from the app's own directory: the app's deps.json and host
            // assets live next to PfCalculator.dll, and `dotnet <dll>` must be
            // invoked from that directory or it cannot resolve them.
            WorkingDirectory = Path.GetDirectoryName(appPath)!,
            FileName = "dotnet",
            Arguments = $"\"{Path.GetFileName(appPath)}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(start)!;
        process.StandardInput.Write(stdin);
        process.StandardInput.Close();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var exitTask = process.WaitForExitAsync(cts.Token);
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await exitTask;
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PfCalculator.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate repo root containing PfCalculator.csproj (looked up from {AppContext.BaseDirectory}).");
    }
}
