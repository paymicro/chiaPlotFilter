using System.Diagnostics;

var forkName = Helper.GetArg("-fork") ?? "chia";
var app = Helper.GetAppPath(forkName);
var totalBuckets = int.TryParse(Helper.GetArg("-u"), out int result) ? result : 128;
var testArg = Helper.GetArg("-test") ?? string.Empty;
var oldTitle = Console.Title ?? string.Empty;
var phase = "0";
var progress = 0d;
var realProgress = 0d;
var table = "";
var phaseTime = new TimeSpan();
var totalSw = new Stopwatch();
var tableSw = new Stopwatch();
var isDash = false;
Process? process = null;
var defaultBgConsoleColor = Console.BackgroundColor;
var bucket = 0;
var d = Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));
var streamWriter = new StreamWriter(Path.Combine(d.FullName, DateTime.Now.ToString("yyyy-dd-MM_HH_mm_ss") + ".log"));
Console.CancelKeyPress += OnConsoleExit;
var lastLineLen = 35;

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

    streamWriter.Close();
    streamWriter.Dispose();

    OnProcessExit(null, null);
}

async Task TestOnLog()
{
    if (!File.Exists(testArg))
    {
        Console.WriteLine($"[Error] Test file '{testArg}' not found.");
        return;
    }

    writeLine($"Testing log: {testArg}");

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
    lastLineLen = 35;
    Console.WriteLine(value);    
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.BackgroundColor = defaultBgConsoleColor;
    updateTitle();
}

void rewriteLine(string value, ConsoleColor color = ConsoleColor.Gray)
{
    if (value == null)
    {
        return;
    }

    Console.ForegroundColor = color;
    Console.BackgroundColor = ConsoleColor.Black;
    var postLen = lastLineLen - value.Length;
    var postfix = postLen > 0 ? new string(' ', postLen) : String.Empty;
    Console.Write($"\r{value}{postfix}");    
    lastLineLen = Math.Max(value.Length, lastLineLen);
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.BackgroundColor = defaultBgConsoleColor;
}

void appendLine(string value, ConsoleColor color = ConsoleColor.Green)
{
    if (value == null)
    {
        return;
    }

    Console.ForegroundColor = color;
    Console.BackgroundColor = ConsoleColor.Black;
    Console.Write(value);
    Console.WriteLine();
    lastLineLen = Math.Max(value.Length, lastLineLen);
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.BackgroundColor = defaultBgConsoleColor;
}

void updateTitle()
{
    var elapsed = $"{totalSw.Elapsed.Hours}h {totalSw.Elapsed.Minutes}m";
    if (totalSw.Elapsed.Days > 0)
    {
        elapsed = $"{totalSw.Elapsed.Days}d {elapsed}";
    }
    var etaString = realProgress > 5 ? $"ETA {(totalSw.Elapsed * 100 / realProgress).TotalHours:F2}h" : "";
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
            "1" => bProgress(1, 4),
            "2" => bProgress(4, 8),
            "3" => bProgress(8, 12),
            "4" => bProgress(12, 16),
            "5" => bProgress(16, 21),
            "6" => bProgress(21, 23),
            "7" => bProgress(23, 29),
            _ => 30,
        },
        "2" => table switch
        {
            "7" => 32,
            "6" => 36,
            "5" => 39,
            "4" => 41,
            "3" => 45,
            "2" => 48,
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

    try
    {
        streamWriter.WriteLine(output);
        streamWriter.Flush();
    }
    catch (Exception)
    {
        // ignored
    }

    updateTitle();

    var _phase = Helper.GetRegexMatch(@"Starting phase (?<res>\d)\/\d:", output);
    if (_phase != null)
    {
        phase = _phase;
        appendLine($"-=[ Phase: {phase} ]=-         started at {DateTime.Now:HH:mm:ss}", ConsoleColor.Yellow);
        bucket = 0;
        return;
    }
    var _progress = Helper.GetRegexMatch(@"Progress update: (?<res>\d\.\d+)", output);
    if (_progress != null)
    {
        progress = double.Parse(_progress) * 100;
        return;
    }
    var _table = Helper.GetRegexMatch(@"", output);
    _table = Helper.GetRegexMatch(@"^Comp\w+ing tables? (?<res>.*)|^Backpropagating on table (?<res>\d+)", output);
    if (_table != null)
    {
        table = _table;
        bucket = 0;
        calcRealProgress();

        switch (phase)
        {
            case "1":
                if (table == "1")
                {
                    rewriteLine($"[P{phase}] Table {table}. | {realProgress:F2}%", ConsoleColor.Gray);
                }
                else if (table == "2")
                {
                    appendLine($" took {tableSw.Elapsed.TotalMinutes:N} min");
                }
                break;
            case "2":
                if (table != "7")
                {
                    appendLine($" took {tableSw.Elapsed.TotalMinutes:N} min");
                }
                rewriteLine($"[P{phase}] Work on table {table} | {realProgress:F2}%", ConsoleColor.Gray);
                break;
        }

        tableSw.Restart();
        return;
    }
    var _tableTime = Helper.GetRegexMatch(@"table time: (?<res>\d*.\d+) seconds", output);
    if (_tableTime != null)
    {
        var tableTime = TimeSpan.FromSeconds(double.Parse(_tableTime));
        appendLine($" took {tableTime.TotalMinutes:N} min");
        return;
    }
    var _phaseTime = Helper.GetRegexMatch(@"Time for phase \d = (?<res>\d+\.\d+) sec", output);
    if (_phaseTime != null)
    {
        if (phase == "2")
        {
            appendLine($" took {tableSw.Elapsed.TotalMinutes:N} min");
        }
        phaseTime = TimeSpan.FromSeconds(double.Parse(_phaseTime));
        var text = $"Phase {phase} took {phaseTime.TotalMinutes:N} minutes";
        if (phase != "4")
        {
            appendLine(text);
        }
        else
        {
            writeLine(text);
        }
        return;
    }
    var qs = Helper.GetRegexMatch(@"(?<res>Bucket \d+ QS.*) force_qs: 0", output);
    if (qs != null)
    {
        writeLine($"[P{phase}] Warning: need more RAM: {qs}", ConsoleColor.Red);
        return;
    }
    var b = Helper.GetRegexMatch(@"Bucket (?<res>\d+)", output);
    if (b != null)
    {
        isDash = true;
        bucket++;
        var tableStr = phase != "4" ? $" Table {table}" : string.Empty;
        calcRealProgress();
        rewriteLine($"[P{phase}]{tableStr}. Bucket: {b} | {realProgress:F2}%", ConsoleColor.Gray);
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