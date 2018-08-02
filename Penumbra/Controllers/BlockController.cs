using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Web;
using System.Web.Mvc;

namespace Penumbra.Controllers
{
    [AllowAnonymous]
    public class BlockController : Controller
    {
        // GET: Block
        /// <summary>
        /// Receives a request from the PAN firewall for a URL request that should be blocked. Also contains data that can be used to identify
        /// whether the user should be allowed to override the block.
        /// </summary>
        /// <returns></returns>
        public ActionResult Index()
        {
            // Setup the model.
            Models.BlockRequestModel model = new Models.BlockRequestModel();

            // Get the User ID of the currently logged in user.
            // model.UserId = User.Identity.Name;

            // Add parameters from the incoming URL block request.
            model.VirtualSystemId = Request.QueryString["vsys"];
            model.CategoryId = Request.QueryString["cat"];
            model.CategoryTitle = Request.QueryString["title"];
            model.RuleName = Request.QueryString["rulename"];
            //model.SourceIP = Request.QueryString["sip"];
            model.SourceIP = Request.UserHostAddress;
            if (model.SourceIP == "::1" || model.SourceIP.StartsWith("127"))
            {
                model.SourceIP = LocalIPAddresses()[0];
            }
            model.Token = Request.QueryString["token"];
            model.Url = Request.QueryString["url"];

            Session[MvcApplication.SESSION_BLOCK_REQUEST_NAME] = model;

            return View(model);
        }

        /// <summary> 
        /// This utility function returns all the IP (v4, not v6) addresses of the local computer.
        /// Via Stephen Cleary - http://blog.stephencleary.com/2009/05/getting-local-ip-addresses.html
        /// </summary> 
        public static List<string> LocalIPAddresses()
        {
            // List of strings with the IP addresses.
            List<string> ips = new List<string>();

            // Get a list of all network interfaces (usually one per network card, dialup, and VPN connection) 
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface network in networkInterfaces)
            {
                // Read the IP configuration for each network 
                IPInterfaceProperties properties = network.GetIPProperties();

                // Each network interface may have multiple IP addresses 
                foreach (IPAddressInformation address in properties.UnicastAddresses)
                {
                    // We're only interested in IPv4 addresses for now 
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    // Ignore loopback addresses (e.g., 127.0.0.1) 
                    if (IPAddress.IsLoopback(address.Address))
                    {
                        continue;
                    }

                    // Ignore Link-local addresses (e.g., 169.254.x.x)
                    if (address.Address.ToString().StartsWith("169.254"))
                    {
                        continue;
                    }

                    ips.Add(address.Address.ToString());
                }
            }

            return ips;
        }
    }
}