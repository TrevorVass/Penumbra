using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Penumbra
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Block",
                url: "php/urladmin.php",
                defaults: new { controller = "Block", action = "Index", vsys = "1", cat = "Unspecified", title = "Unknown", rulename = "Unknown Rule", sip = "unknown", token = "unknown", url="unknown" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Override", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
