using System.ComponentModel;
using System.Diagnostics;

var forkName = Helper.GetArg("-fork") ?? "chia";
var app = Helper.GetAppPath(forkName);
var testArg = Helper.GetArg("-test") ?? string.Empty;
var oldTitle = Console.Title ?? string.Empty;
var phaseTitleTop = 0;
Process? process = null;
var defaultBgConsoleColor = Console.BackgroundColor;
var d = Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));
var pathToLog = Path.Combine(d.FullName, DateTime.Now.ToString("yyyy-dd-MM_HH_mm_ss") + ".log");
var streamWriter = new StreamWriter(pathToLog);
var firstCol = 45;
var titleFormat = 0;

var data = new PlotData
{
    TotalBuckets = int.TryParse(Helper.GetArg("-u"), out int result) ? result : 128
};

if (app != null)
{
    data.PropertyChanged += OnProperyChanged;
    Console.CancelKeyPress += OnConsoleExit;

    _ = KeyPresses();
    AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);

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

void OnProperyChanged(object? sender, PropertyChangedEventArgs e)
{
    updateTitle();
    switch (e.PropertyName)
    {
        case nameof(PlotData.Phase):
            writeLine($"-=[ Phase: {data.Phase} ]=-         started at {DateTime.Now:HH:mm:ss}", ConsoleColor.Yellow);
            break;
        case nameof(PlotData.Bucket):
            var tableStr = data.Phase != 4 ? $" Table {data.Table}" : string.Empty;
            rewriteLine($"[P{data.Phase}]{tableStr}. Bucket: {data.Bucket:D3} | {data.RealProgress:F2}%", ConsoleColor.Gray);
            break;
        case nameof(PlotData.Table):
            switch (data.Phase)
            {
                case 1:
                    switch (data.Table)
                    {
                        case "1":
                            rewriteLine($"[P{data.Phase}] Table {data.Table}. | {data.RealProgress:F2}%", ConsoleColor.Gray);
                            break;
                        case "2":
                            appendLine($" took {data.tableSw.Elapsed.TotalMinutes:N} min");
                            break;
                    }
                    break;
                case 2:
                    switch (data.Table)
                    {
                        case "7": break;
                        default: appendLine($" took {data.tableSw.Elapsed.TotalMinutes:N} min"); break;
                    }
                    rewriteLine($"[P{data.Phase}] Work on table {data.Table} | {data.RealProgress:F2}%", ConsoleColor.Gray);
                    break;
            }
            phaseTitleTop = Console.CursorTop;
            break;
        case nameof(PlotData.PhaseTime):
            switch (data.Phase)
            {
                case 2:
                    appendLine($" took {data.tableSw.Elapsed.TotalMinutes:N} min");
                    break;
                case 4:
                    Console.WriteLine();
                    break;
            }
            writeLine($"Phase {data.Phase} took {data.PhaseTime.TotalMinutes:N} minutes");
            break;
        case nameof(PlotData.TableTime):
            appendLine($" took {data.TableTime.TotalMinutes:N} min");
            break;
    }
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
        await Task.Delay(200);
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

        await process.WaitForExitAsync();
    }
}

async Task KeyPresses()
{
    while (true)
    {
        await Task.Delay(100);
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.U:
                    titleFormat++;
                    if (titleFormat > 3)
                    {
                        titleFormat = 0;
                    }
                    break;
                case ConsoleKey.H:
                    rewriteLine("H presed");
                    break;
                case ConsoleKey.Q:
                    rewriteLine("For exit press Ctrl-C or close dialog");
                    break;
                case ConsoleKey.L:
                    Process.Start("explorer.exe", pathToLog);
                    break;
            }
        }
        updateTitle();
    }
}

void writeLine(string value, ConsoleColor color = ConsoleColor.Green, int left = -1)
{
    Console.ForegroundColor = color;
    Console.BackgroundColor = ConsoleColor.Black;
    if (left > 0)
    {
        Console.SetCursorPosition(left, Console.GetCursorPosition().Top);
    }
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
    var postLen = firstCol - value.Length;
    var postfix = postLen > 0 ? new string(' ', postLen) : String.Empty;
    Console.Write($"\r{value}{postfix}");
    Console.Write('\r');
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
    Console.SetCursorPosition(firstCol, Console.CursorTop);
    Console.Write(value);
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.BackgroundColor = defaultBgConsoleColor;
}

void updateTitle()
{
    switch (titleFormat)
    {
        default:
            Console.Title = string.Join(" | ",
                new[] {
                    $"P{data.Phase}",
                    $"{data.RealProgress:F2}%",
                    data.GetElapsed(),
                    data.GetRemain(),
                    oldTitle,
                }.Where(s => !string.IsNullOrEmpty(s)));
            break;
        case 1:
            Console.Title = string.Join(" | ",
                new[] {
                    $"P{data.Phase}",
                    $"{data.RealProgress:F2}%",
                    data.GetElapsed(),
                    data.GetRemain(),
                }.Where(s => !string.IsNullOrEmpty(s)));
            break;
        case 2:
            Console.Title = oldTitle;
            break;
        case 3:
            Console.Title = string.Join(" | ",
                new[] { $"{data.RealProgress:F2}%",
                    data.GetTotal(),
                    data.GetETA()
                }.Where(s => !string.IsNullOrEmpty(s))); ;
            break;
    }
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

    var _phase = Helper.GetRegexMatch(@"Starting phase (?<res>\d)\/\d:", output);
    if (_phase != null)
    {
        data.Phase = int.TryParse(_phase, out result) ? result : 0;        
        return;
    }
    var _table = Helper.GetRegexMatch(@"", output);
    _table = Helper.GetRegexMatch(@"^Comp\w+ing tables? (?<res>.*)|^Backpropagating on table (?<res>\d+)", output);
    if (_table != null)
    {
        data.Table = _table;
        return;
    }
    var _tableTime = Helper.GetRegexMatch(@"table time: (?<res>\d*.\d+) seconds", output);
    if (_tableTime != null)
    {
        data.TableTime = TimeSpan.FromSeconds(double.Parse(_tableTime));
        return;
    }
    var _phaseTime = Helper.GetRegexMatch(@"Time for phase \d = (?<res>\d+\.\d+) sec", output);
    if (_phaseTime != null)
    {
        data.PhaseTime = TimeSpan.FromSeconds(double.Parse(_phaseTime));
        return;
    }
    var qs = Helper.GetRegexMatch(@"(?<res>Bucket \d+ QS.*) force_qs: 0", output);
    if (qs != null)
    {
        writeLine($"[P{data.Phase}] Warning: need more RAM: {qs}", ConsoleColor.Red);
        return;
    }
    var b = Helper.GetRegexMatch(@"Bucket (?<res>\d+)", output);
    if (b != null)
    {
        data.Bucket++;
    }
}

void OutputDataReceived(object? sender, DataReceivedEventArgs? e)
{
    FilterOutput(e?.Data);
}

void OnProcessExit(object? sender, EventArgs? e)
{
    writeLine("Plotter has ended.", ConsoleColor.Yellow);
    writeLine($"Plot time: {data.plotSw.Elapsed.TotalHours:N} hours");
    Environment.Exit(0);
}

void OnConsoleExit(object? sender, ConsoleCancelEventArgs? e)
{
    writeLine("Canceling...", ConsoleColor.Yellow);
    if (process != null)
    {
        Console.WriteLine("---------------------------------------------");
        Console.WriteLine($"Kill process {process.ProcessName}? (Y/n)");
        var q = Console.ReadLine();
        if (q.ToLowerInvariant().FirstOrDefault() == 'y')
        {
            writeLine($"Process {process.ProcessName} | id {process.Id} killed.", ConsoleColor.Yellow);
            process.Kill();
        }
    }
    writeLine($"Plot time: {data.plotSw.Elapsed.TotalHours:N} hours");
    Environment.Exit(0);
}

void CurrentDomain_ProcessExit(object? sender, EventArgs e)
{
    OnConsoleExit(sender, null);
}