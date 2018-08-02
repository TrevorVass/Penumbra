using Galactic.Sql;
using Galactic.Sql.MSSql;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Web;
using System.Web.Security;
using System.Web.Mvc;
using Penumbra.Models;

namespace Penumbra.Controllers
{
    [Authorize]
    public class OverrideController : Controller
    {
        /// <summary>
        /// The name of the key for block model data in the TempData dictionary when passing values between actions.
        /// </summary>
        private const string BLOCK_MODEL_TEMPDATA_KEY = "blockModel";

        // GET: Override
        /// <summary>
        /// Allows a user to select whether they want to override the web filter.
        /// </summary>
        /// <returns></returns>
        public ActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Overrides the web filter.
        /// </summary>
        /// <returns></returns>
        public ActionResult Do()
        {
            // Get the block request object from session state.
            BlockRequestModel model = (BlockRequestModel)Session[MvcApplication.SESSION_BLOCK_REQUEST_NAME];

            // Get the User ID of the currently logged in user.
            model.UserId = User.Identity.Name;

            if (!string.IsNullOrWhiteSpace(MvcApplication.overridePayloadTemplate))
            {
                try
                {
                    StringBuilder payload = new StringBuilder(MvcApplication.overridePayloadTemplate);
                    payload.Replace("%IP%", model.SourceIP);

                    // Ignore certificate checking for this connection. (Self-signed certs. Note: this is global to the application.)
                    RemoteCertificateValidationCallback previousCallback = ServicePointManager.ServerCertificateValidationCallback;
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                    // Make a GET request to the firewall with the override XML payload.
                    string url = "https://" + MvcApplication.FIREWALL_HOSTNAME + "/api/?type=user-id&key=" + MvcApplication.PAN_API_KEY + "&cmd=" + Url.Encode(payload.ToString());
                    WebRequest request = WebRequest.Create(url);
                    WebResponse response = request.GetResponse();
                    
                    // Reinstate certificate checking.
                    ServicePointManager.ServerCertificateValidationCallback = previousCallback;

                    // Add the override to the list of active overrides in the database.
                    MSStoredProcedure upsert = new MSStoredProcedure(MvcApplication.databaseConnectionString, MvcApplication.STORED_PROCEDURE_UPSERT_OVERRIDE, MvcApplication.eventLog);
                    upsert.AddNVarCharParameter(MvcApplication.STORED_PROCEDURE_PARAM_IP, model.SourceIP, MvcApplication.STORED_PROCEDURE_PARAM_IP_LENGTH, StoredProcedure.ParameterType.In);
                    upsert.AddNVarCharParameter(MvcApplication.STORED_PROCEDURE_PARAM_USERNAME, model.UserId, MvcApplication.STORED_PROCEDURE_PARAM_USERNAME_LENGTH, StoredProcedure.ParameterType.In);
                    if (upsert.ExecuteNonQuery() > 0)
                    {
                        // Log the override in the event log.
                        MvcApplication.Log(new Galactic.EventLog.Event(MvcApplication.EVENT_LOG_SOURCE_NAME, DateTime.Now, Galactic.EventLog.Event.SeverityLevels.Information,
                            MvcApplication.EVENT_LOG_CATEGORY_OVERRIDE, "User: " + model.UserId + " IP: " + model.SourceIP + " URL: " + model.Url));
                    }
                    else
                    {
                        // Log an error in the event log.
                        MvcApplication.Log(new Galactic.EventLog.Event(MvcApplication.EVENT_LOG_SOURCE_NAME, DateTime.Now, Galactic.EventLog.Event.SeverityLevels.Error,
                            MvcApplication.EVENT_LOG_CATEGORY_OVERRIDE, "Unable to save override to database. User: " + model.UserId + " IP: " + model.SourceIP + " URL: " + model.Url));
                    }
                }
                catch (Exception e)
                {
                    // Catch any exceptions here.
                    // Redirect to an error page, where they can try again.
                    MvcApplication.Log(new Galactic.EventLog.Event(MvcApplication.EVENT_LOG_SOURCE_NAME, DateTime.Now, Galactic.EventLog.Event.SeverityLevels.Error,
                           MvcApplication.EVENT_LOG_CATEGORY_OVERRIDE, "Unhandled exception.\nMessage: " + e.Message + "\nInner Exception: " + e.InnerException + "\nStack Trace: " + e.StackTrace));
                    return null;
                }
            }

            // Redirect the user to the site they initially requested.
            TempData[BLOCK_MODEL_TEMPDATA_KEY] = model;
            return RedirectToAction("SiteRedirect");
        }

        /// <summary>
        /// Handles the redirection of the user to the site after the override process is complete.
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult SiteRedirect()
        {
            if (User.Identity.IsAuthenticated)
            {
                // Sign the user out of the override interface, if they were logged in.
                FormsAuthentication.SignOut();
            }

            if (TempData.ContainsKey(BLOCK_MODEL_TEMPDATA_KEY))
            {
                BlockRequestModel model = (BlockRequestModel)TempData[BLOCK_MODEL_TEMPDATA_KEY];
                if (model != null)
                {
                    return View(model);
                }
                else
                {
                    // Redirect to an error page, where they can try again.
                    return null;
                }
            }
            else
            {
                // Redirect to an error page, where they can try again.
                return null;
            }
        }
    }
}