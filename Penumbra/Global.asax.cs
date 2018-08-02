using Galactic.ActiveDirectory;
using Galactic.Configuration;
using Galactic.EventLog;
using Galactic.EventLog.Sql;
using Galactic.FileSystem;
using Galactic.Sql;
using Galactic.Sql.MSSql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Routing;
using System.Text;
using System.Threading;

namespace Penumbra
{
    public class MvcApplication : System.Web.HttpApplication
    {
        // The directory containing configuration items used by the application.
        private const string CONFIG_ITEM_DIRECTORY = @"ConfigurationItems\";

        // The name of configuraiton item containing the application's general configuration.
        private const string GENERAL_CONFIGURATION_ITEM_NAME = "Penumbra";

        // The name of the configuration item that contains the connection information required to access Active Directory.
        private const string ACTIVE_DIRECTORY_CONFIGURATION_ITEM_NAME = "ActiveDirectory";

        // The name of the configuration item that contains the connection string for the application's database.
        private const string DATABASE_CONNECTION_STRING_CONFIGURATION_ITEM_NAME = "DatabaseConnectionString";

        // Application relative path to the override payload template file.
        private const string OVERRIDE_PAYLOAD_TEMPLATE_PATH = @"App_Data\payloads\override.xml";

        // The name of the session variable that contains the block request object, supplied by the firewall.
        public const string SESSION_BLOCK_REQUEST_NAME = "BlockRequest";

        // The name of the source of events for this application's event log.
        public const string EVENT_LOG_SOURCE_NAME = "Penumbra";

        // List of categories that correspond to various events in the event log.
        public const string EVENT_LOG_CATEGORY_GENERAL = "OverrideWebApplication";  // General logging for the application.
        public const string EVENT_LOG_CATEGORY_OVERRIDE = "Override";               // Overrides logged by the application.

        // Stored procedure names.
        public const string STORED_PROCEDURE_UPSERT_OVERRIDE = "UpsertOverride";    // Upserts overrides into the list of active overrides.

        // Stored procedure parameter names, lengths, etc.
        public const string STORED_PROCEDURE_PARAM_IP = "ip";                       // An IPv4 address.
        public const int STORED_PROCEDURE_PARAM_IP_LENGTH = 15;

        public const string STORED_PROCEDURE_PARAM_USERNAME = "userName";           // A username.
        public const int STORED_PROCEDURE_PARAM_USERNAME_LENGTH = 256;

        // Global application variables.
        public static ActiveDirectory activeDirectory;
        public static string databaseConnectionString;
        public static SqlEventLog eventLog;             // Logs application events to a SQL database.
        public static string overridePayloadTemplate;   // The XML payload template used when initiating overrides with the firewall.

        // Variables loaded from general configuration item.

        // The name of the Active Directory group that is given administration rights over the application.
        public static string ADMIN_GROUP_NAME = "";

        // The name of the Active Directory group that is given rights to override the URL filter via the application.
        public static string OVERRIDER_GROUP_NAME = "";

        // The PAN Firewall API Key used by the application.
        public static string PAN_API_KEY = "";

        // The hostname of the firewall.
        public static string FIREWALL_HOSTNAME = "";

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // Setup a connection with Active Directory.
            activeDirectory = new ActiveDirectory(HttpRuntime.AppDomainAppPath + CONFIG_ITEM_DIRECTORY, ACTIVE_DIRECTORY_CONFIGURATION_ITEM_NAME);

            // Get the connection string for the database.
            databaseConnectionString = (new ConfigurationItem(HttpRuntime.AppDomainAppPath + CONFIG_ITEM_DIRECTORY, DATABASE_CONNECTION_STRING_CONFIGURATION_ITEM_NAME, true)).Value;

            // Create a SQL Event log instance for logging application events.
            eventLog = new SqlEventLog(databaseConnectionString, typeof(MSStoredProcedure));

            if (eventLog == null)
            {
                throw new ApplicationException("Unable to initialize event log.");
            }
            
            Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Error, EVENT_LOG_CATEGORY_GENERAL,
                        "Override web application started."));

            // Read the application's configuration from it's configuration item.
            ReadApplicationConfig();

            // Get the override payload template.
            string payloadFilePath = HttpRuntime.AppDomainAppPath + OVERRIDE_PAYLOAD_TEMPLATE_PATH;
            if (File.Exists(payloadFilePath))
            {
                try
                {
                    overridePayloadTemplate = File.ReadAllAsText(payloadFilePath);
                    
                    Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information, EVENT_LOG_CATEGORY_GENERAL,
                        "Loaded override payload from " + payloadFilePath + " ."));
                }
                catch
                {
                    Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Error, EVENT_LOG_CATEGORY_GENERAL,
                        "Unable to load override payload from " + payloadFilePath + " ."));
                    throw new ApplicationException("Unable to load override paylod. Check log for more details.");
                }
            }
        }

        /// <summary>
        /// Read the application's configuration information from it's configuration item.
        /// </summary>
        protected void ReadApplicationConfig()
        {
            ConfigurationItem configItem = new ConfigurationItem(HttpRuntime.AppDomainAppPath + CONFIG_ITEM_DIRECTORY, GENERAL_CONFIGURATION_ITEM_NAME, true);

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
                        case "Admin Group Name":
                            ADMIN_GROUP_NAME = value;
                            break;
                        case "Overrider Group Name":
                            OVERRIDER_GROUP_NAME = value;
                            break;
                        case "PAN API Key":
                            PAN_API_KEY = value;
                            break;
                        case "Firewall Hostname":
                            FIREWALL_HOSTNAME = value;
                            break;
                    }
                }
            }

            // Check that all application configuration values were loaded.
            if (string.IsNullOrWhiteSpace(ADMIN_GROUP_NAME) ||
                string.IsNullOrWhiteSpace(OVERRIDER_GROUP_NAME) ||
                string.IsNullOrWhiteSpace(PAN_API_KEY) ||
                string.IsNullOrWhiteSpace(FIREWALL_HOSTNAME))
            {
                Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Error, EVENT_LOG_CATEGORY_GENERAL,
                    "Unable to load application values from general configuration from configuration item named " +
                    GENERAL_CONFIGURATION_ITEM_NAME + " within " + CONFIG_ITEM_DIRECTORY + " ."));
                throw new ApplicationException("Unable to load application values from general configuration. Check log for more details.");
            }
            else
            {
                Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information, EVENT_LOG_CATEGORY_GENERAL,
                    "Loaded from general configuration: Admin Group Name = " + ADMIN_GROUP_NAME));
                Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information, EVENT_LOG_CATEGORY_GENERAL,
                    "Loaded from general configuration: Overrider Group Name = " + OVERRIDER_GROUP_NAME));

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

                Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information, EVENT_LOG_CATEGORY_GENERAL,
                        "Loaded from general configuration: PAN API KEY = " + maskedApiKey));

                Log(new Event(EVENT_LOG_SOURCE_NAME, DateTime.Now, Event.SeverityLevels.Information, EVENT_LOG_CATEGORY_GENERAL,
                    "Loaded from general configuration: Firewall Hostname = " + FIREWALL_HOSTNAME));
            }
        }

        /// <summary>
        /// Logs an event to the event log.
        /// </summary>
        /// <param name="eventToLog">The event to Log.</param>
        /// <returns>True if the event was logged, false otherwise.</returns>
        public static bool Log(Event eventToLog)
        {
            // It's necessary to sleep to iterate the timestamp so the log will accept the entry.
            Thread.Sleep(10);

            return eventLog.Log(eventToLog);
        }
    }
}
