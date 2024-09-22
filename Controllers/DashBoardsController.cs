using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using ZB_FEPMS.Action_Filters;
using ZB_FEPMS.Models;

namespace ZB_FEPMS.Controllers
{

    [RBAC]
    [NoCache]
    public class DashBoardsController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
        public ActionResult OfficerDashboard()
        {
            return View();
        }

        public ActionResult ManagerDashboard()
        {
            return View();
        }

        public JsonResult ManagerDashboardDetails()
        {
            string result = "";
            try
            {
                db.Configuration.ProxyCreationEnabled = false;
                ManagerDashboardDetails managerDashboardDetails = new ManagerDashboardDetails();
                managerDashboardDetails.POUpdateAmountRequestCount = db.tblPermitAmounts
                    .Where(tpa => tpa.tbl_lu_Status.name.Equals("Pending")
                    && tpa.tblPermit.tblSerialNumberShelf.SerialNumberType.Equals("PO"))
                    .Count();
                managerDashboardDetails.ImportPermitUpdateAmountRequestCount = db.tblPermitAmounts
                    .Where(tpa => tpa.tbl_lu_Status.name.Equals("Pending")
                    && tpa.tblPermit.tblSerialNumberShelf.SerialNumberType.Equals("IMP"))
                    .Count();
                managerDashboardDetails.InvisiblePaymentUpdateAmountRequestCount = db.tblApplicationAmounts
                    .Where(taa => taa.tbl_lu_Status.name.Equals("Pending"))
                    .Count();
                JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
                result = javaScriptSerializer.Serialize(managerDashboardDetails);
            }
            catch (Exception) { }
            return Json(result ?? "", JsonRequestBehavior.AllowGet);
        }
    }
}