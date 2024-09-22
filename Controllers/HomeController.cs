using System.Web.Mvc;
using ZB_FEPMS.Action_Filters;
using ZB_FEPMS.Models;


namespace ZB_FEPMS.Controllers
{
    [RBAC]
    [NoCache]
    public class HomeController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult Unauthorised()
        {
            return View();
        }

        public void toggleNavType()
        {
            string navType = System.Web.HttpContext.Current.Session["navType"].ToString();
            if (navType.Equals("nav-md"))
            {
                System.Web.HttpContext.Current.Session["navType"] = "nav-sm";
            }
            else if (navType.Equals("nav-sm"))
            {
                System.Web.HttpContext.Current.Session["navType"] = "nav-md";
            }
        }

        public ActionResult Logout()
        {
            string url = "https://auth.zemenbank.com/auth/realms/zemen/protocol/openid-connect/logout?"
                          + "client_id=" + Session["clientId"].ToString();
            return Redirect(url);
        }

    }
}