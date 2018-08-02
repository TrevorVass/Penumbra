using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Penumbra.Models
{
    public class BlockRequestModel
    {
        /// <summary>
        /// The User ID of the user using the override control panel.
        /// </summary>
        public string UserId;

        /// <summary>
        /// A number that uniquely identifies the firewall that has forwarded the request.
        /// </summary>
        public string VirtualSystemId;

        /// <summary>
        /// The id of the category that the site to be blocked belongs to.
        /// </summary>
        public string CategoryId;

        /// <summary>
        /// The human readable title of the category that the site to be blocked belongs to.
        /// </summary>
        public string CategoryTitle;

        /// <summary>
        /// The name of the policy on the firewall that initiated the request.
        /// </summary>
        public string RuleName;

        /// <summary>
        /// The source IP address of the client to making the request for the URL.
        /// </summary>
        public string SourceIP;

        /// <summary>
        /// A token of unknown portent...
        /// </summary>
        public string Token;

        /// <summary>
        /// The URL of the website to be blocked.
        /// </summary>
        public string Url;
        
    }
}