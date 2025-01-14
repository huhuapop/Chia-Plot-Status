﻿using ChiaPlotStatusLib.Logic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChiaPlotStatus
{
    /**
     * Parses a log file and generates PlotLogs for each plotting process in the log file
     * (multiple PlotLogs per file if plot create --num n is used)
     * Tails the log file if it is still being written. Call Parse() before each access
     */
    public class PlotLogFileParser
    {
        private TailLineEmitter TailLineEmitter { get; }
        protected List<PlotLog> PlotLogs { get; } = new List<PlotLog>();
        public string LogFile;
        public string LogFolder;
        public bool firstRead = true;
        public bool lineRead = true;
        public bool closed = false;
        public PlotParserCache cache;
        public DateTime? lastGrown;

        public PlotLogFileParser(string path, bool closeOnEndOfFile, ref PlotParserCache cache)
        {
            this.cache = cache;
            this.LogFile = path;
            this.LogFolder = path.Substring(0, path.LastIndexOf(Path.DirectorySeparatorChar));
            this.TailLineEmitter = new TailLineEmitter(path, closeOnEndOfFile, (line) =>
            {
                CurrentPlotLog().IsLastLineTempError = false;
                CurrentPlotLog().LastLogLine = line.Trim();
                switch (line)
                {
                    case var _ when plotSizeRg.IsMatch(line):
                        CurrentPlotLog().PlotSize = int.Parse(plotSizeRg.Matches(line)[0].Groups[1].Value);
                        break;
                    case var _ when bufferSizeRg.IsMatch(line):
                        CurrentPlotLog().Buffer = int.Parse(bufferSizeRg.Matches(line)[0].Groups[1].Value);
                        break;
                    case var _ when bucketsRg.IsMatch(line):
                        CurrentPlotLog().Buckets = int.Parse(bucketsRg.Matches(line)[0].Groups[1].Value);
                        break;
                    case var _ when currentBucketRg.IsMatch(line):
                        CurrentPlotLog().CurrentBucket = 1 + int.Parse(currentBucketRg.Matches(line)[0].Groups[1].Value);
                        break;
                    case var _ when threadsRg.IsMatch(line):
                        CurrentPlotLog().Threads = int.Parse(threadsRg.Matches(line)[0].Groups[1].Value);
                        break;
                    case var _ when pidRG.IsMatch(line):
                        CurrentPlotLog().PID = int.Parse(pidRG.Matches(line)[0].Groups[1].Value);
                        break;
                    case var _ when startDateRg.IsMatch(line):
                        var dateTimeStr = startDateRg.Matches(line)[0].Groups[1].Value;
                        CurrentPlotLog().StartDate = DateTime.ParseExact(dateTimeStr, "ddd MMM d HH:mm:ss yyyy", new System.Globalization.CultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces);
                        break;
                    case var _ when plotNameRg.IsMatch(line):
                        CurrentPlotLog().PlotName = plotNameRg.Matches(line)[0].Groups[1].Value;
                        break;
                    case var _ when phase1Rg.IsMatch(line):
                        CurrentPlotLog().Phase1Seconds = int.Parse(phase1Rg.Matches(line)[0].Groups[1].Value);
                        CurrentPlotLog().Phase1Cpu = double.Parse(phase1Rg.Matches(line)[0].Groups[2].Value, CultureInfo.InvariantCulture);
                        CurrentPlotLog().CurrentBucket = 0;
                        break;
                    case var _ when phase1Table.IsMatch(line):
                        CurrentPlotLog().Phase1Table = int.Parse(phase1Table.Matches(line)[0].Groups[1].Value);
                        CurrentPlotLog().CurrentTable = CurrentPlotLog().Phase1Table;
                        CurrentPlotLog().CurrentPhase = 1;
                        break;
                    case var _ when phase2Rg.IsMatch(line):
                        CurrentPlotLog().Phase2Seconds = int.Parse(phase2Rg.Matches(line)[0].Groups[1].Value);
                        CurrentPlotLog().Phase2Cpu = double.Parse(phase2Rg.Matches(line)[0].Groups[2].Value, CultureInfo.InvariantCulture);
                        CurrentPlotLog().CurrentBucket = 0;
                        break;
                    case var _ when phase2Table.IsMatch(line):
                        CurrentPlotLog().Phase2Table = int.Parse(phase2Table.Matches(line)[0].Groups[1].Value);
                        CurrentPlotLog().CurrentTable = CurrentPlotLog().Phase2Table;
                        CurrentPlotLog().CurrentPhase = 2;

                        break;
                    case var _ when phase3Rg.IsMatch(line):
                        CurrentPlotLog().Phase3Seconds = int.Parse(phase3Rg.Matches(line)[0].Groups[1].Value);
                        CurrentPlotLog().Phase3Cpu = double.Parse(phase3Rg.Matches(line)[0].Groups[2].Value, CultureInfo.InvariantCulture);
                        CurrentPlotLog().CurrentBucket = 0;
                        CurrentPlotLog().CurrentPhase = 4;
                        break;
                    case var _ when phase3Table.IsMatch(line):
                        CurrentPlotLog().Phase3Table = int.Parse(phase3Table.Matches(line)[0].Groups[1].Value);
                        CurrentPlotLog().CurrentTable = CurrentPlotLog().Phase3Table;
                        CurrentPlotLog().CurrentPhase = 3;
                        break;
                    case var _ when phase4Rg.IsMatch(line):
                        CurrentPlotLog().Phase4Seconds = int.Parse(phase4Rg.Matches(line)[0].Groups[1].Value);
                        CurrentPlotLog().Phase4Cpu = double.Parse(phase4Rg.Matches(line)[0].Groups[2].Value, CultureInfo.InvariantCulture);
                        CurrentPlotLog().CurrentBucket = 0;
                        CurrentPlotLog().CurrentPhase = 5;
                        break;
                    case var _ when copyTime.IsMatch(line):
                        CurrentPlotLog().CopyTimeSeconds = int.Parse(copyTime.Matches(line)[0].Groups[1].Value);
                        break;
                    case var _ when totalTimeRg.IsMatch(line):
                        var curPlot = CurrentPlotLog();
                        curPlot.TotalSeconds = int.Parse(totalTimeRg.Matches(line)[0].Groups[1].Value);
                        curPlot.CurrentPhase = 6;
                        if (curPlot.StartDate != null)
                            curPlot.FinishDate = ((DateTime)curPlot.StartDate).AddSeconds(curPlot.TotalSeconds);
                        break;
                    case var _ when approximateWorkingSpace.IsMatch(line):
                        CurrentPlotLog().ApproximateWorkingSpace = approximateWorkingSpace.Matches(line)[0].Groups[1].Value;
                        break;
                    case var _ when destinationDirectory.IsMatch(line):
                        CurrentPlotLog().DestDrive = destinationDirectory.Matches(line)[0].Groups[1].Value;
                        break;
                    case var _ when finalFileSize.IsMatch(line):
                        CurrentPlotLog().FinalFileSize = finalFileSize.Matches(line)[0].Groups[1].Value;
                        break;
                    case var _ when queueSize.IsMatch(line):
                        CurrentPlotLog().QueueSize = int.Parse(queueSize.Matches(line)[0].Groups[1].Value);
                        break;
                    case var _ when writePloblemRg.IsMatch(line):
                    case var _ when readPloblemRg.IsMatch(line):
                    case var _ when copyPloblemRg.IsMatch(line):
                    case var _ when couldNotOpenFile.IsMatch(line):
                        CurrentPlotLog().IsLastLineTempError = true;
                        CurrentPlotLog().Errors++;
                        break;
                    case var _ when caughtPlottingError.IsMatch(line):
                        CurrentPlotLog().CaughtPlottingError = true;
                        break;
                    case var _ when tmpFolders.IsMatch(line):
                        var match = tmpFolders.Matches(line)[0];
                        var plotLog = CurrentPlotLog();
                        if (plotLog.Tmp1Drive != null) // this is a new plot in the same logfile (--num n used)
                        {
                            plotLog = NextPlotLog();
                        }
                        plotLog.Tmp1Drive = match.Groups[1].Value;
                        plotLog.Tmp2Drive = match.Groups[2].Value;
                        break;
                    default:
                        break;
                }
                var cPlotLog = CurrentPlotLog();
                cPlotLog.LogFile = this.LogFile;
                cPlotLog.LogFolder = this.LogFolder;
                cPlotLog.UpdateProgress();
                lineRead = true;
            });
        }

        /**
         * Can be called as often as needed as it does not reparse what was already processed.
         */
        public List<PlotLog> ParsePlotLog()
        {
            if (closed)
                return PlotLogs;
            var fromCache = cache.Get(this);
            if (fromCache != null)
            {
                Debug.WriteLine("Plotlog was already in cache. Not reading again.");
                firstRead = false;
                Close();
                foreach (var plotLog in fromCache)
                    PlotLogs.Add(plotLog);
                return PlotLogs;
            }
            lineRead = false;
            this.TailLineEmitter.ReadMore();
            CurrentPlotLog().FileLastWritten = File.GetLastWriteTime(this.LogFile);
            if (CurrentPlotLog().FileLastWritten != null && ((DateTime.Now - (DateTime)CurrentPlotLog().FileLastWritten).TotalDays > 1d))
            {
                cache.Add(this, PlotLogs);
                Close();
            }
            if (lineRead && !firstRead)
                lastGrown = DateTime.Now;
            if (lastGrown != null)
                CurrentPlotLog().FileLastWritten = lastGrown;
            firstRead = false;

            return PlotLogs;
        }

        private void Close()
        {
            if (closed)
                return;
            Debug.WriteLine("PlotLog " + LogFile + " has not been updated for more than a day. Closing file.");
            this.TailLineEmitter.Close();
            closed = true;
        }

        private PlotLog CurrentPlotLog()
        {
            if (PlotLogs.Count == 0)
                PlotLogs.Add(new PlotLog());
            return PlotLogs[PlotLogs.Count - 1];
        }

        private PlotLog NextPlotLog()
        {
            var oldPlotLog = CurrentPlotLog();
            oldPlotLog.IsLastInLogFile = false;
            var newPlotLog = new PlotLog();
            // when plot create --num n is used parameters stay the same
            newPlotLog.Buckets = oldPlotLog.Buckets;
            newPlotLog.Threads = oldPlotLog.Threads;
            newPlotLog.Buffer = oldPlotLog.Buffer;
            newPlotLog.Tmp1Drive = oldPlotLog.Tmp1Drive;
            newPlotLog.Tmp2Drive = oldPlotLog.Tmp2Drive;
            newPlotLog.DestDrive = oldPlotLog.DestDrive;
            newPlotLog.LogFile = oldPlotLog.LogFile;
            newPlotLog.LogFolder = oldPlotLog.LogFolder;
            newPlotLog.PlaceInLogFile = oldPlotLog.PlaceInLogFile + 1;
            newPlotLog.QueueSize = oldPlotLog.QueueSize;
            PlotLogs.Add(newPlotLog);
            return newPlotLog;
        }


        // interesting data from logfiles as regex
        public static Regex plotSizeRg = new Regex("^Plot size is: (\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex bufferSizeRg = new Regex("^Buffer size is: (\\d+)MiB", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex bucketsRg = new Regex("^Using (\\d+) buckets", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex threadsRg = new Regex("^Using (\\d+) threads of stripe size (\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex pidRG = new Regex("^Process ID is: (\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex startDateRg = new Regex("^Starting phase 1/4: Forward Propagation into tmp files\\.\\.\\. (.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex phase1Rg = new Regex("^Time for phase 1 = (\\d+)\\.\\d+ seconds. CPU \\((\\d+\\.\\d+)%\\) ", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex phase2Rg = new Regex("^Time for phase 2 = (\\d+)\\.\\d+ seconds. CPU \\((\\d+\\.\\d+)%\\) ", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex phase3Rg = new Regex("^Time for phase 3 = (\\d+)\\.\\d+ seconds. CPU \\((\\d+\\.\\d+)%\\) ", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex phase4Rg = new Regex("^Time for phase 4 = (\\d+)\\.\\d+ seconds. CPU \\((\\d+\\.\\d+)%\\) ", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex copyTime = new Regex("^Copy time = (\\d+)\\.\\d+ seconds", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex totalTimeRg = new Regex("^Total time = (\\d+)\\.\\d+ seconds", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex plotNameRg = new Regex("^Renamed final file from \".+\" to (\".+\")", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex currentBucketRg = new Regex("^\\tBucket (\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex phase1Table = new Regex("^Computing table (\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex phase2Table = new Regex("^Backpropagating on table (\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex phase3Table = new Regex("^Compressing tables (\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex tmpFolders = new Regex("^Starting plotting progress into temporary dirs: (.*) and (.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex writePloblemRg = new Regex("^Only wrote \\d+ of \\d+ bytes at", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex couldNotOpenFile = new Regex("^Could not open", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex readPloblemRg = new Regex("^Only read \\d+ of \\d+ bytes at", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex copyPloblemRg = new Regex("^Could not copy ", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex approximateWorkingSpace = new Regex("^Approximate working space used \\(without final file\\): (\\d+\\.\\d+ .*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex finalFileSize = new Regex("^Final File size: (\\d+\\.\\d+ .*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex destinationDirectory = new Regex("^Final Directory is: (.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex caughtPlottingError = new Regex("^Caught plotting error: .*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex queueSize = new Regex("Starting plot \\d+.(\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
