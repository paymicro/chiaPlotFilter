using System.ComponentModel;
using System.Diagnostics;

public class PlotData
{
    int _phase = 0;
    string _table = "0";
    int _bucket = 0;
    TimeSpan _phaseTime = TimeSpan.Zero;
    TimeSpan _tableTime = TimeSpan.Zero;
    readonly Stopwatch _tableSw = new();
    readonly Stopwatch _plotSw = new();

    public TimeSpan TableTime { get; private set; } = TimeSpan.Zero;
    public TimeSpan PlotTime { get; private set; } = TimeSpan.Zero;

    public int TotalBuckets { get; set; }

    public int Phase
    {
        get
        {
            return _phase;
        }
        set
        {
            _phase = value;
            _bucket = 0;

            OnPropertyChanged(nameof(Phase));
            if (value == 1)
            {
                PlotTime = _plotSw.Elapsed;
                _plotSw.Restart();
            }
        }
    }

    public double RealProgress
    {
        get
        {
            return Phase switch
            {
                1 => Table switch
                {
                    "1" => GetProgress(1, 4),
                    "2" => GetProgress(4, 8),
                    "3" => GetProgress(8, 12),
                    "4" => GetProgress(12, 16),
                    "5" => GetProgress(16, 21),
                    "6" => GetProgress(21, 23),
                    "7" => GetProgress(23, 29),
                    _ => 30,
                },
                2 => Table switch
                {
                    "7" => 32,
                    "6" => 36,
                    "5" => 39,
                    "4" => 41,
                    "3" => 45,
                    "2" => 48,
                    _ => 50,
                },
                3 => Table switch
                {
                    "1 and 2" => GetProgress(50, 57),
                    "2 and 3" => GetProgress(57, 65),
                    "3 and 4" => GetProgress(65, 73),
                    "4 and 5" => GetProgress(73, 82),
                    "5 and 6" => GetProgress(82, 91),
                    "6 and 7" => GetProgress(91, 96.5),
                    _ => 96.5,
                },
                4 => GetProgress(96.5, 100),
                _ => 0,
            };
        }
    }

    public string Table
    {
        get
        {
            return _table;
        }
        set
        {
            _table = value;
            _bucket = 0;
            TableTime = _tableSw.Elapsed;
            _tableSw.Restart();
            OnPropertyChanged(nameof(Table));            
        }
    }

    public int Bucket
    {
        get
        {
            return _bucket;
        }
        set
        {
            _bucket = value;
            OnPropertyChanged(nameof(Bucket));
        }
    }

    public TimeSpan PhaseTime
    {
        get
        {
            return _phaseTime;
        }
        set
        {
            _phaseTime = value;
            OnPropertyChanged(nameof(PhaseTime));
        }
    }

    public TimeSpan TableTimeLog
    {
        get
        {
            return _tableTime;
        }
        set
        {
            _tableTime = value;
            OnPropertyChanged(nameof(TableTimeLog));
        }
    }

    public string GetElapsed()
    {
        var elapsed = $"{_plotSw.Elapsed.Hours}h {_plotSw.Elapsed.Minutes}m";
        if (_plotSw.Elapsed.Days > 0)
        {
            elapsed = $"{_plotSw.Elapsed.Days}d {elapsed}";
        }
        return $"elapsed {elapsed}";
    }

    public string GetTotal()
    {
        return RealProgress > 5 ? $"total {(_plotSw.Elapsed * 100 / RealProgress).TotalHours:F2}h" : "";
    }

    public string GetETA()
    {
        return RealProgress > 5 ? $"ETA {DateTime.Now + _plotSw.Elapsed * 100 / RealProgress - _plotSw.Elapsed:g}h" : "";
    }

    public string GetRemain()
    {
        return RealProgress > 5 ? $"remain {(_plotSw.Elapsed * 100 / RealProgress - _plotSw.Elapsed).TotalHours:F2}h" : "";
    }

    double GetProgress(double from, double to)
    {
        var b = Phase switch
        {
            3 => Table switch
            {
                "1 and 2" => Bucket * 0.51d,
                "2 and 3" => Bucket * 0.50d,
                "3 and 4" => Bucket * 0.44d,
                "4 and 5" => Bucket * 0.44d,
                "5 and 6" => Bucket * 0.43d,
                "6 and 7" => Bucket * 0.406d,
                _ => Bucket * 0.4d,
            },
            _ => Bucket,
        };
        return from + (to - from) * b / TotalBuckets;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
