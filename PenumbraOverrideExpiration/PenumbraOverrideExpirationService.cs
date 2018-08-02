using Galactic.Configuration;
using Galactic.EventLog;
using Galactic.EventLog.Sql;
using Galactic.FileSystem;
using Galactic.Sql;
using Galactic.Sql.MSSql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Timers;

namespace PenumbraOverrideExpiration
{
    public partial class PenumbraOverrideExpirationService : ServiceBase
    {
        // The nuber of milliseconds in a minute.
        private const int MILLISECONDS_IN_MINUTE = 60000;

        // The number of interval (in minutes) that the service checks for overrides that need to be unregistered.
        private const int CHECK_INTERVAL_MINUTES = 1;

        // The directory containing configuration items used by the application.
        private const string CONFIG_ITEM_DIRECTORY = @"ConfigurationItems\";

        // The name of configuraiton item containing the application's general configuration.
        private const string GENERAL_CONFIGURATION_ITEM_NAME = "Penumbra";

        // The name of the configuration item that contains the connection string for the application's database.
        private const string DATABASE_CONNECTION_STRING_CONFIGURATION_ITEM_NAME = "DatabaseConnectionString";

        // Application relative path to the unregister override payload template file.
        private const string OVERRIDE_PAYLOAD_TEMPLATE_PATH = @"payloads\unregister.xml";

        // The name of the source of events for this application's event log.
        public const string EVENT_LOG_SOURCE_NAME = "Penumbra";

        // List of categories that correspond to various events in the event log.
        public const string EVENT_LOG_CATEGORY_GENERAL = "ExpirationService";       // General logging for the application.
        public const string EVENT_LOG_CATEGORY_OVERRIDE = "Override";               // Overrides logged by the application.

        // Stored procedure names.
        public const string STORED_PROCEDURE_DELETE_OVERRIDE = "DeleteOverride";                // Deletes override from the list of active overrides.
        public const string STORED_PROCEDURE_GET_EXPIRED_OVERRIDES = "GetExpiredOverrides";     // Gets overrides that are older than the expiration period from the list of active overrides.

        // Stored procedure parameter names, lengths, etc.
        public const string STORED_PROCEDURE_PARAM_OVERRIDE_DURATION = "overrideDuration";  // The length in minutes that overrides can remain active.
        public const string STORED_PROCEDURE_PARAM_IP = "ip";                               // An IPv4 address.
        public const int STORED_PROCEDURE_PARAM_IP_LENGTH = 15;

        // SQL row field names.
        public const string SQL_FIELD_IP = "ip";    // An IPv4 address.
        public const string SQL_FIELD_USERNAME = "userName";    // A username.

        // Global application variables.
        public static string databaseConnectionString;
        public static SqlEventLog eventLog;                 // Logs application events to a SQL database.
        public static string unregisterPayloadTemplate;     // The XML payload template used when unregistering overrides with the firewall.
        public static bool executeLoop = true;              // Whether the service's main execution loop should run.
        private System.Timers.Timer timer = null;           // A timer used to initiate the application's logic on a schedule.

        // Variables loaded from general configuration item.

        // The PAN Firewall API Key used by the application.
        public static string PAN_API_KEY = "";

        // The hostname of the firewall.
        public static string FIREWALL_HOSTNAME = "";

        // The duration of an override in minutes.
        public static int OVERRIDE_DURATION = 0;
        public const int DEFAULT_OVERRIDE_DURATION = 120;   // A default value if one isn't read from the configuration item.

        public PenumbraOverrideExpirationService()
        {
            InitializeComponent();
        }

        internal void TestStartupAndStop(string[] args)
        {
            this.OnStart(args);
            Console.ReadLine();
            this.OnStop();
        }

        protected override void OnStart(string[] args)
        {
            // Launch the debugger if in DEBUG configuration.
#if DEBUG
            if (!Debugger.IsAttached)
            {
                // Sleep for 5 seconds if the debugger isn't attached.
                Thread.Sleep(5000);
            }
#endif

            // Initialize the service.
            InitializeService();

            // Create a timer to expire the overrides every CHECK_INTERVAL_MINUTES.
            timer = new System.Timers.Timer(CHECK_INTERVAL_MINUTES * MILLISECONDS_IN_MINUTE);
            timer.Elapsed += new ElapsedEventHandler(this.ExpireOverrides);
            timer.Enabled = true;
        }

        protected override void OnStop()
        {
            // Signal the timer to stop running.
            timer.Enabled = false;

            eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information, EVENT_LOG_CATEGORY_GENERAL,
                        "Expiration service stopped."));
        }

        /// <summary>
        /// Loads configuration data, and readies the service for execution.
        /// </summary>
        protected void InitializeService()
        {
            // Get the connection string for the database.
            databaseConnectionString = (new ConfigurationItem(AppDomain.CurrentDomain.BaseDirectory + CONFIG_ITEM_DIRECTORY, DATABASE_CONNECTION_STRING_CONFIGURATION_ITEM_NAME, true)).Value;

            // Create a SQL Event log instance for logging application events.
            eventLog = new SqlEventLog(databaseConnectionString, typeof(MSStoredProcedure));

            if (eventLog == null)
            {
                throw new ApplicationException("Unable to initialize event log.");
            }

            eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information, EVENT_LOG_CATEGORY_GENERAL,
                        "Expiration service started."));

            // Read the application's configuration from it's configuration item.
            ReadApplicationConfig();

            // Get the override payload template.
            string payloadFilePath = AppDomain.CurrentDomain.BaseDirectory + OVERRIDE_PAYLOAD_TEMPLATE_PATH;

            if (File.Exists(payloadFilePath))
            {
                try
                {
                    unregisterPayloadTemplate = File.ReadAllAsText(payloadFilePath);

                    eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information, EVENT_LOG_CATEGORY_GENERAL,
                        "Loaded unregister payload from " + payloadFilePath + " . "));
                }
                catch
                {
                    eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Error, EVENT_LOG_CATEGORY_GENERAL,
                        "Unable to load unregister payload from " + payloadFilePath + " ."));
                    throw new ApplicationException("Unable to load unregister paylod. Check log for more details.");
                }
            }
        }

        /// <summary>
        /// Read the application's configuration information from it's configuration item.
        /// </summary>
        protected void ReadApplicationConfig()
        {
            ConfigurationItem configItem = new ConfigurationItem(AppDomain.CurrentDomain.BaseDirectory + CONFIG_ITEM_DIRECTORY, GENERAL_CONFIGURATION_ITEM_NAME, true);

            // Process each line of the configuration item's value.
            foreach (string line in configItem.Value.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                // Find the first equals sign in the line. This splits the key from the value on the line.
                int separatorIndex = line.IndexOf('=');

                // Check that the line was properly separated.
                if (separatorIndex != 0)
                {
                    string key = line.Substring(0, separatorIndex);
                    string value = line.Substring(separatorIndex + 1, line.Length - (separatorIndex + 1));

                    // Match the key to the application's configuration values.
                    switch (key)
                    {
                        case "PAN API Key":
                            PAN_API_KEY = value;
                            break;
                        case "Firewall Hostname":
                            FIREWALL_HOSTNAME = value;
                            break;
                        case "Override Duration (min)":
                            try
                            {
                                OVERRIDE_DURATION = Int32.Parse(value);
                            }
                            catch
                            {
                                // There was an error parsing the value from the configuration item. Set it to a default value.
                                OVERRIDE_DURATION = DEFAULT_OVERRIDE_DURATION;

                                eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Error, EVENT_LOG_CATEGORY_GENERAL,
                                    "Unable to load override duration from general configuration from configuration item named " +
                                    GENERAL_CONFIGURATION_ITEM_NAME + " within " + CONFIG_ITEM_DIRECTORY + " . Using default of: " + DEFAULT_OVERRIDE_DURATION
                                    + " instead."));
                            }
                            break;
                    }
                }
            }

            // Check that all application configuration values were loaded.
            if (string.IsNullOrWhiteSpace(PAN_API_KEY) ||
                string.IsNullOrWhiteSpace(FIREWALL_HOSTNAME))
            {
                eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Error, EVENT_LOG_CATEGORY_GENERAL,
                    "Unable to load application values from general configuration from configuration item named " +
                    GENERAL_CONFIGURATION_ITEM_NAME + " within " + CONFIG_ITEM_DIRECTORY + " ."));
                throw new ApplicationException("Unable to load application values from general configuration. Check log for more details.");
            }
            else
            {
                // Mask the API key in the log.
                StringBuilder maskedApiKey = new StringBuilder();
                if (PAN_API_KEY.Length >= 4)
                {
                    int maskedLength = PAN_API_KEY.Length - 4;
                    for (int i = 0; i < maskedLength; i++)
                    {
                        maskedApiKey.Append('*');
                    }
                    maskedApiKey.Append(PAN_API_KEY.Substring(maskedLength, 4));
                }
                else
                {
                    maskedApiKey.Append("****");
                }

                eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information, EVENT_LOG_CATEGORY_GENERAL,
                    "Loaded from general configuration: PAN API KEY = " + maskedApiKey));

                eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information, EVENT_LOG_CATEGORY_GENERAL,
                    "Loaded from general configuration: Firewall Hostname = " + FIREWALL_HOSTNAME));
            }
        }

        /// <summary>
        /// Expires overrides that are older than the allowed override duration. Run on an interval.
        /// </summary>
        protected void ExpireOverrides(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            try
            {
                // Get the list of overrides that need to be expired.
                MSStoredProcedure getExpiredOverrides = new MSStoredProcedure(databaseConnectionString, STORED_PROCEDURE_GET_EXPIRED_OVERRIDES, eventLog);
                getExpiredOverrides.AddInt32Parameter(STORED_PROCEDURE_PARAM_OVERRIDE_DURATION, OVERRIDE_DURATION, StoredProcedure.ParameterType.In);
                List<SqlRow> expiredOverrides = getExpiredOverrides.Execute();

                // Expire each override found.
                foreach (SqlRow expiredOverride in expiredOverrides)
                {
                    string ip = (string)expiredOverride[SQL_FIELD_IP];
                    string userName = (string)expiredOverride[SQL_FIELD_USERNAME];

                    StringBuilder payload = new StringBuilder(unregisterPayloadTemplate);
                    payload.Replace("%IP%", ip);

                    // Ignore certificate checking for this connection. (Self-signed certs. Note: this is global to the application.)
                    RemoteCertificateValidationCallback previousCallback = ServicePointManager.ServerCertificateValidationCallback;
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                    // Make a GET request to the firewall with the unregister XML payload.
                    string url = "https://" + FIREWALL_HOSTNAME + "/api/?type=user-id&key=" + PAN_API_KEY + "&cmd=" + WebUtility.UrlEncode(payload.ToString());
                    WebRequest request = WebRequest.Create(url);
                    using (WebResponse response = request.GetResponse())
                    {
                        // Disposes of the response once complete.
                    }

                    // Reinstate certificate checking.
                    ServicePointManager.ServerCertificateValidationCallback = previousCallback;

                    // Delete the override from the list of active overrides in the database.
                    MSStoredProcedure deleteOverride = new MSStoredProcedure(databaseConnectionString, STORED_PROCEDURE_DELETE_OVERRIDE, eventLog);
                    deleteOverride.AddNVarCharParameter(STORED_PROCEDURE_PARAM_IP, ip, STORED_PROCEDURE_PARAM_IP_LENGTH, StoredProcedure.ParameterType.In);
                    if (deleteOverride.ExecuteNonQuery() > 0)
                    {
                        // Log the override in the event log.
                        eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information,
                            EVENT_LOG_CATEGORY_OVERRIDE, "Expire - User: " + userName + " IP: " + ip));
                    }
                    else
                    {
                        // Log an error in the event log.
                        eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Error,
                            EVENT_LOG_CATEGORY_OVERRIDE, "Unable to expire IP in database. IP: " + ip));
                    }
                }
            }
            catch (Exception e)
            {
                // Log any exceptions.
                eventLog.Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Error,
                        EVENT_LOG_CATEGORY_GENERAL, "Service - Unhandled exception.\nMessage: " + e.Message + "\nInner Exception: " + e.InnerException + "\nStack Trace: " + e.StackTrace));
            }
        }
    }
}
