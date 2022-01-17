using System.Diagnostics;

var forkName = Helper.GetArg("-fork") ?? "chia";
var app = Helper.GetAppPath(forkName);
var totalBuckets = int.TryParse(Helper.GetArg("-u"), out int result) ? result : 128;
var testArg = Helper.GetArg("-test") ?? "";
var oldTitle = Console.Title ?? string.Empty;
var phase = "0";
var progress = 0d;
var realProgress = 0d;
var table = "";
var phaseTime = new TimeSpan();
var totalSw = new Stopwatch();
var phaseSw = new Stopwatch();
var isDash = false;
Process? process = null;
var defaultBgConsoleColor = Console.BackgroundColor;
var bucket = 0;
var d = Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));
var streamWriter = new StreamWriter(Path.Combine(d.FullName, DateTime.Now.ToString("yyyy-dd-MM_HH_mm_ss") + ".log"));
Console.CancelKeyPress += OnConsoleExit;

if (app != null)
{
    totalSw.Restart();
    if (string.IsNullOrEmpty(testArg))
    {
        await startJob(app, string.Join(" ", Environment.GetCommandLineArgs().Skip(1).ToArray()));
    }
    else
    {
        await TestOnLog();
    }

    streamWriter?.Close();
    streamWriter?.Dispose();

    OnProcessExit(null, null);
}

async Task TestOnLog()
{
    if (!File.Exists(testArg))
    {
        Console.WriteLine($"[Error] Test file '{testArg}' not found.");
        return;
    }

    using var testReader = new StreamReader(testArg);
    var line = testReader.ReadLine();
    while (!string.IsNullOrEmpty(line))
    {
        FilterOutput(line);
        line = testReader.ReadLine();
        await Task.Delay(20);
    }
    return;
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
        process.Exited += OnProcessExit;
        process.Start();
        process.BeginOutputReadLine();

        AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);

        await process.WaitForExitAsync();
    }
}

void writeLine(string value, ConsoleColor color = ConsoleColor.Green)
{
    Console.ForegroundColor = color;
    Console.BackgroundColor = ConsoleColor.Black;
    if (isDash)
    {
        Console.WriteLine();
    }
    isDash = false;
    Console.WriteLine(value);    
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.BackgroundColor = defaultBgConsoleColor;
    updateTitle();
}

void updateTitle()
{
    var elapsed = $"{totalSw.Elapsed.Hours}h {totalSw.Elapsed.Minutes}m";
    if (totalSw.Elapsed.Days > 0)
    {
        elapsed = $"{totalSw.Elapsed.Days}d {elapsed}";
    }
    var etaString = realProgress > 5 ? $"ETA {(totalSw.Elapsed / realProgress).TotalHours:F2}h" : "";
    Console.Title = string.Join(" | ", new[] { $"P{phase}", $"{realProgress:F2}%", $"elapsed {elapsed}", etaString, oldTitle }.Where(s => !string.IsNullOrEmpty(s)));
}

double bProgress(double from, double to)
{
    var b = phase switch
    {
        "3" => table switch
        {
            "1 and 2" => bucket * 0.51d,
            "2 and 3" => bucket * 0.50d,
            "3 and 4" => bucket * 0.44d,
            "4 and 5" => bucket * 0.44d,
            "5 and 6" => bucket * 0.43d,
            "6 and 7" => bucket * 0.406d,
            _ => bucket * 0.4d,
        },
        _ => bucket,
    };
    return from + (to - from) * b / totalBuckets;
}

void calcRealProgress()
{
    realProgress = phase switch
    {
        "1" => table switch
        {
            "1" => bProgress(1, 4.5),
            "2" => bProgress(4.5, 9),
            "3" => bProgress(9, 13),
            "4" => bProgress(13, 17),
            "5" => bProgress(17, 22),
            "6" => bProgress(22, 24),
            "7" => bProgress(24, 30),
            _ => 30,
        },
        "2" => table switch
        {
            "7" => bProgress(31, 34),
            "6" => bProgress(34, 38),
            "5" => bProgress(38, 41),
            "4" => bProgress(41, 43),
            "3" => bProgress(43, 45),
            "2" => bProgress(45, 50),
            _ => 50,
        },
        "3" => table switch
        {
            "1 and 2" => bProgress(50, 57),
            "2 and 3" => bProgress(57, 65),
            "3 and 4" => bProgress(65, 73),
            "4 and 5" => bProgress(73, 82),
            "5 and 6" => bProgress(82, 91),
            "6 and 7" => bProgress(91, 96.5),
            _ => 96.5,
        },
        "4" => table switch
        {
            _ => bProgress(96.5, 100),
        },
        _ => progress,
    };
}

void FilterOutput(string? output)
{
    if (string.IsNullOrWhiteSpace(output))
    {
        return;
    }

    streamWriter.WriteLine(output);
    streamWriter.Flush();
    calcRealProgress();
    updateTitle();

    var _phase = Helper.GetRegexMatch(@"Starting phase (?<res>\d)\/\d:", output);
    if (_phase != null)
    {
        phase = _phase;
        writeLine($"-=[ Phase: {phase} ]=-         started at {DateTime.Now:HH:mm:ss}");
        bucket = 0;
        phaseSw.Restart();
        return;
    }
    var _progress = Helper.GetRegexMatch(@"Progress update: (?<res>\d\.\d+)", output);
    if (_progress != null)
    {
        progress = double.Parse(_progress) * 100;
        writeLine($"[P{phase}] Progress {progress:F2} % | corrected {realProgress:F2} %");
        return;
    }
    var _table = Helper.GetRegexMatch(@"^Comp\w+ing tables? (?<res>.*)|^Backpropagating on table (?<res>\d+)", output);
    if (_table != null)
    {
        table = _table;
        bucket = 0;
        writeLine($"[P{phase}] Work on table {table}");
        return;
    }
    var _tableTime = Helper.GetRegexMatch(@"table time: (?<res>\d*.\d+) seconds", output);
    if (_tableTime != null)
    {
        var tableTime = TimeSpan.FromSeconds(double.Parse(_tableTime));
        writeLine($"[P{phase}] Table {table} took {tableTime.TotalMinutes:N} min");
        return;
    }
    var _phaseTime = Helper.GetRegexMatch(@"Time for phase \d = (?<res>\d+\.\d+) sec", output);
    if (_phaseTime != null)
    {
        phaseTime = TimeSpan.FromSeconds(double.Parse(_phaseTime));
        writeLine($"Phase {phase} took {phaseTime.TotalMinutes:N} minutes");
        return;
    }
    var qs = Helper.GetRegexMatch(@"(?<res>Bucket \d+ QS.*) force_qs: 0", output);
    if (qs != null)
    {
        writeLine($"[P{phase}] Warning: need more RAM: {qs}", ConsoleColor.Yellow);
        return;
    }

    // any bucket output is '-'    
    var b = Helper.GetRegexMatch(@"Bucket (?<res>\d+)", output);
    if (b != null)
    {
        isDash = true;
        bucket++;
        Console.Write("-");
    }
}

void OutputDataReceived(object? sender, DataReceivedEventArgs? e)
{
    FilterOutput(e?.Data);
}

void OnProcessExit(object? sender, EventArgs? e)
{
    writeLine("Plotter has ended.", ConsoleColor.Yellow);
    writeLine($"Total time: {totalSw.Elapsed.TotalHours:N} hours");
    Environment.Exit(0);
}

void OnConsoleExit(object? sender, ConsoleCancelEventArgs? e)
{
    writeLine("Canceling...", ConsoleColor.Yellow);
    if (process != null)
    {
        writeLine($"Process {process.ProcessName} | id {process.Id} killed.", ConsoleColor.Yellow);
        process.Kill();
    }
    writeLine($"Total time: {totalSw.Elapsed.TotalHours:N} hours");
    Environment.Exit(0);
}

void CurrentDomain_ProcessExit(object? sender, EventArgs e)
{
    OnConsoleExit(sender, null);
}