# chiaPlotFilter

Console application for proxy output of a standart chia plotter.

# Usage

Use the same arguments as for the standard plotter. The program will find its location by itself and start plotting.
For example:
```
chiaPlotFilter.exe plots create -k 32 -b 7400 -u 64 -r 10 -t D:\temp\ -d E:\ -c xch123456 -f 123456;
```

For forks use additional `-fork` argument
```
-fork chives
```
