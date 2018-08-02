using Penumbra.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Web.Routing;

namespace Penumbra.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        //
        // GET: /SignIn/

        [AllowAnonymous]
        public ActionResult SignIn()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public ActionResult SignIn(LoginModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                if (Membership.ValidateUser(model.UserName, model.Password))
                {
                    // The user's credentials are valid.
                    FormsAuthentication.SetAuthCookie(model.UserName, false);
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        // If they supply a URL to return to, redirect them there.
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        // By default send them to the default application page.
                        return Redirect(Url.Action("Index", "Override"));
                    }
                }
                else
                {
                    // The user's credentials are invalid.
                    // Redirect them back to the sign-in page with an error.
                    ViewBag.error_invalidCredentials = true;
                    return View(model);
                }
            }

            // The ModelState isn't valid.
            // Present the sign-in form again.
            return View(model);
        }

        public ActionResult SignOut()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("SignIn");
        }
    }
}