using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Configuration;

namespace KeepAware
{
    public partial class NodeService : ServiceBase
    {

        private static NodeProcess App; //TODO: support multiple processes!

        //EventLog Names
        private const string SourceName = "KeepAware Service";
        private const string LogName = "KeepAware Log";


        /// <summary>
        /// Service manages the node.js processes
        /// </summary>
        public NodeService()
        {
            InitializeComponent();

            //Create event log if it doesnt exist:
            if (!System.Diagnostics.EventLog.SourceExists(SourceName))
            {
                System.Diagnostics.EventLog.CreateEventSource(SourceName, LogName);
            }

            //Initialize event log
            KeepAwareEventLog.Source = SourceName;
            KeepAwareEventLog.Log = LogName;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                //Get config settings from Thingflow.exe.config
                string exePath = System.Configuration.ConfigurationManager.AppSettings["exe"];
                string appPath = System.Configuration.ConfigurationManager.AppSettings["app"];
                string logPath = System.Configuration.ConfigurationManager.AppSettings["log"];
                string watchFilter = System.Configuration.ConfigurationManager.AppSettings["watch"];

                //Start up the app
                App = new NodeProcess(exePath, appPath, logPath, SourceName, LogName, watchFilter);
                App.StartProcess();
            }
            catch (Exception ex)
            {
                //Problem with service
                KeepAwareEventLog.WriteEntry("Failed to Start::" + ex.Message,EventLogEntryType.Error);
            }

        }

        protected override void OnStop()
        {
            try
            {
                //Shut it down
                App.StopProcess();
            }
            catch (Exception ex)
            {
                //Problem with stop
                KeepAwareEventLog.WriteEntry("Failed to Stop::" + ex.Message, EventLogEntryType.Error);
            }
        }
    }
}
