﻿using System.Diagnostics;

var forkName = Helper.GetFork();
var app = Helper.GetAppPath(forkName);
var phase = "0";
var progress = 0d;
var phaseTime = new TimeSpan();
var sw = new Stopwatch();
var isDash = false;
Process? process = null;
Tuple<TimeSpan, string> lastOutput = new(TimeSpan.Zero, "");

if (app != null)
{
    sw.Restart();
    Console.CancelKeyPress += OnExit;
    await startJob(app, string.Join(" ", Environment.GetCommandLineArgs().Skip(1).ToArray()));
    OnChiaExit(null, null);
}


async Task startJob(string path, string args)
{
    writeLine($"Start: {forkName} {args}");
    using (process = new Process())
    {
        process.StartInfo = new ProcessStartInfo(path)
        {
            CreateNoWindow = true,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
        };
        process.OutputDataReceived += OutputDataReceived;
        process.Exited += OnChiaExit;
        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();
    }
}

void writeLine(string value, ConsoleColor color = ConsoleColor.Green)
{
    Console.ForegroundColor = color;
    if (isDash)
    {
        Console.WriteLine();
    }
    isDash = false;
    Console.WriteLine(value);    
    Console.ForegroundColor = ConsoleColor.Gray;
}

void FilterOutput(string? output)
{
    if (string.IsNullOrWhiteSpace(output))
    {
        return;
    }

    var _phase = Helper.GetRegexMatch(@"Starting phase (?<res>\d)\/\d:", output);
    if (_phase != null)
    {
        phase = _phase;
        writeLine($"-=[ Phase: {phase} ]=-");
        return;
    }
    var _progress = Helper.GetRegexMatch(@"Progress update: (?<res>\d\.\d+)", output);
    if (_progress != null)
    {
        progress = double.Parse(_progress) * 100;
        writeLine($"[P{phase}] Progress {progress:N}%");
        return;
    }
    var _phaseTime = Helper.GetRegexMatch(@"Time for phase \d = (?<res>\d+\.\d+) sec", output);
    if (_phaseTime != null)
    {
        phaseTime = TimeSpan.FromSeconds(double.Parse(_phaseTime));
        writeLine($"Time of [P{phase}]: {phaseTime}");
        return;
    }

    var qs = Helper.GetRegexMatch(@"(?<res>Bucket \d{2} QS.*) force_qs: 0", output);
    if (qs != null)
    {
        writeLine($"[P{phase}] Warning: need more RAM: {qs}", ConsoleColor.Yellow);
        return;
    }

    // any other output is -
    Console.Write("-");
    isDash = true;
}

void OutputDataReceived(object? sender, DataReceivedEventArgs? e)
{
    FilterOutput(e?.Data);
}

void OnChiaExit(object? sender, EventArgs? e)
{
    writeLine("Plotter has ended.");
    writeLine($"Total time: {sw.Elapsed.TotalHours:N} hours");
    Environment.Exit(0);
}

void OnExit(object? sender, ConsoleCancelEventArgs? e)
{
    writeLine("Canceling...", ConsoleColor.Yellow);
    if (process != null)
    {
        writeLine($"Process {process.ProcessName} | id {process.Id} killed.", ConsoleColor.Yellow);
        process.Kill();
    }
    writeLine($"Total time: {sw.Elapsed.TotalHours:N} hours");
    Environment.Exit(-1);
}