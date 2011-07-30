using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;

namespace KeepAware
{

    /// <summary>
    /// Runs a single windowless node.js app as a process, with auto-restart, eventlogging, and console output redirect.
    /// </summary>
    class NodeProcess
    {

        private Process AppProcess;
        private EventLog AppLog;
        private StreamWriter AppOutput;
        private FileSystemWatcher AppWatcher;

        private string ExePath;
        private string AppPath;
        private string LogPath;
        private string WatchPath;
        private string WatchFilter;
        private bool ExitNormal;

        /// <summary>
        /// Runs a single windowless node.js app as a process, with auto-restart, eventlogging, and console output redirect.
        /// </summary>
        /// <param name="exePath">The full path of the node.exe (example: "c:\node\node.exe")</param>
        /// <param name="appPath">The full path of the Node application entry point (example: "c:\myapp\app.js")</param>
        /// <param name="logPath">The full path of the Node application log file (example: "c:\myapp\app.log")</param>
        /// <param name="sourceName">The EventLog Source</param>
        /// <param name="logName">The EventLog Name</param>
        /// <param name="watchFilter"></param>
        
        public NodeProcess(string exePath, string appPath, string logPath, string sourceName, string logName, string watchFilter)
        {
            AppLog = new EventLog();
            AppLog.Source = sourceName;
            AppLog.Log = logName;

            ExePath = exePath;
            AppPath = appPath;
            LogPath = logPath;
            WatchPath = appPath.Substring(0, appPath.LastIndexOf("\\") + 1);
            WatchFilter = watchFilter;
        }

        #region Public Methods

        /// <summary>
        /// Starts the Node.js process
        /// </summary>
        public void StartProcess(bool Initialize = true)
        {

            AppProcess = new Process();


            if (Initialize)
            {
                //node console.log output redirect
                AppOutput = new StreamWriter(LogPath, true);

                //Automatically write output to the log
                AppOutput.AutoFlush = true;

                //Watch for changed files
                AppWatcher = new FileSystemWatcher(WatchPath);
                AppWatcher.NotifyFilter = NotifyFilters.LastWrite;
                AppWatcher.Filter = WatchFilter;
                AppWatcher.Changed += new FileSystemEventHandler(AppWatcher_Changed);
                AppWatcher.Created += new FileSystemEventHandler(AppWatcher_Changed);
                AppWatcher.Deleted += new FileSystemEventHandler(AppWatcher_Changed);
                AppWatcher.Renamed += new RenamedEventHandler(AppWatcher_Renamed);

                //Notify EventLog Process is trying to start
                AppLog.WriteEntry("Starting node.js Process", EventLogEntryType.Information);
            }
            else
            {
                AppLog.WriteEntry("Restarting node.js Process", EventLogEntryType.Information);
            }

            try
            {
                //For Exited event
                AppProcess.EnableRaisingEvents = true;

                //Settings for node exe and script to run
                AppProcess.StartInfo.FileName = ExePath;
                AppProcess.StartInfo.Arguments = AppPath;

                //Suppress window and prep for console.log output capture
                AppProcess.StartInfo.CreateNoWindow = true;
                AppProcess.StartInfo.UseShellExecute = false;
                AppProcess.StartInfo.RedirectStandardOutput = true;
                AppProcess.StartInfo.RedirectStandardError = false;

                //Elevate permissions to run the node.exe
                AppProcess.StartInfo.Verb = "runas";

                //Redirect output from console.log
                AppProcess.OutputDataReceived += new DataReceivedEventHandler(AppProcess_OutputDataReceived);

                //Listen for abnormal exits to restart
                AppProcess.Exited += new EventHandler(AppProcess_Exited);

                //Start it up!
                ExitNormal = false;
                AppProcess.Start();

                //Start console output capture
                AppProcess.BeginOutputReadLine();

            }
            catch (Exception)
            {
                //Unknown exception, throw it back to the service
                throw;
            }

            //Notify EventLog that the Process is (re)started
            if (Initialize)
            {
                AppLog.WriteEntry("Node.js Process Started", EventLogEntryType.Information);
            }
            else
            {
                AppLog.WriteEntry("Node.js Process Restarted", EventLogEntryType.Information);
            }        
        
        }


        /// <summary>
        /// Stops the Node.js process
        /// </summary>
        public void StopProcess()
        {

            try
            {
                AppLog.WriteEntry("Stopping Node Process", EventLogEntryType.Information);
                if (AppProcess != null && !AppProcess.HasExited)
                {
                    //Make sure it doesnt restart
                    ExitNormal = true;

                    //Shut it down!
                    AppProcess.Kill();
                }
                AppOutput.Close();
                AppLog.WriteEntry("Node.js Process Stopped", EventLogEntryType.Information);
            }
            catch (Exception)
            {
                //Unknown exception, throw it back to the service
                throw;
            }
            finally
            {
                //Clean up
                AppProcess.Dispose();
                AppOutput.Dispose();
            }

        }

        #endregion

        #region Events

        /// <summary>
        /// Listen for source file renamed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AppWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            //Restart the process
            ExitNormal = true;
            AppProcess.Kill();
            StartProcess(false);
        }
        
        /// <summary>
        /// Listen for source files Changed, Created, or Deleted.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AppWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            //Restart the process
            ExitNormal = true;
            AppProcess.Kill();
            StartProcess(false);
        }

        /// <summary>
        /// Listen for abnormal exit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AppProcess_Exited(object sender, EventArgs e)
        {
            if (!ExitNormal)
            {
                //Put error in event log and restart!
                AppLog.WriteEntry("Process terminated abnormally, attempting to restart", EventLogEntryType.Error);
                AppProcess.Dispose();
                StartProcess(false);
            }
        }

        /// <summary>
        /// Listen for console.log output
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AppProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                // Add the text to the log.
                AppOutput.WriteLine(e.Data);
            }
        }

        #endregion

    }
}
