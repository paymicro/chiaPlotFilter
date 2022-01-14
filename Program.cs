﻿using System.Diagnostics;

var forkName = Helper.GetFork();
var app = Helper.GetAppPath(forkName);
var oldTitle = Console.Title ?? string.Empty;
var phase = "0";
var progress = 0d;
var table = "";
var phaseTime = new TimeSpan();
var totalSw = new Stopwatch();
var phaseSw = new Stopwatch();
var isDash = false;
Process? process = null;

var d = Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));
var streamWriter = new StreamWriter(Path.Combine(d.FullName, DateTime.Now.ToString("yyyy-dd-MM_HH_mm_ss") + ".log"));
streamWriter.AutoFlush = true;

if (app != null)
{
    totalSw.Restart();    
    Console.CancelKeyPress += OnExit;
    await startJob(app, string.Join(" ", Environment.GetCommandLineArgs().Skip(1).ToArray()));
    streamWriter.Flush();
    streamWriter.Close();
    streamWriter.Dispose();
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

    Console.Title = $"{progress}% | elapsed {totalSw.Elapsed.TotalHours:N}h | {oldTitle}";

    streamWriter.WriteLine(output);

    var _phase = Helper.GetRegexMatch(@"Starting phase (?<res>\d)\/\d:", output);
    if (_phase != null)
    {
        phase = _phase;
        writeLine($"-=[ Phase: {phase} ]=-         started at {DateTime.Now:HH:mm:ss}");
        phaseSw.Restart();
        return;
    }
    var _progress = Helper.GetRegexMatch(@"Progress update: (?<res>\d\.\d+)", output);
    if (_progress != null)
    {
        progress = double.Parse(_progress) * 100;
        writeLine($"[P{phase}] Progress {progress}%");
        return;
    }
    var _table = Helper.GetRegexMatch(@"^Comp\w+ing tables? (?<res>.*)", output);
    if (_table != null)
    {
        table = _table;
        writeLine($"[P{phase}] Work on table {table}");
        return;
    }
    var _tableTime = Helper.GetRegexMatch(@"table time: (?<res>\d*.\d+) seconds", output);
    if (_tableTime != null)
    {
        var tableTime = TimeSpan.FromSeconds(double.Parse(_tableTime));
        writeLine($"[P{phase}] Table {table} took {tableTime.TotalMinutes:N}");
        return;
    }
    var _phaseTime = Helper.GetRegexMatch(@"Time for phase \d = (?<res>\d+\.\d+) sec", output);
    if (_phaseTime != null)
    {
        phaseTime = TimeSpan.FromSeconds(double.Parse(_phaseTime));
        writeLine($"Phase {phase} took {phaseTime.TotalMinutes:N} minutes");
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
    writeLine($"Total time: {totalSw.Elapsed.TotalHours:N} hours");
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
    writeLine($"Total time: {totalSw.Elapsed.TotalHours:N} hours");
    Environment.Exit(-1);
}