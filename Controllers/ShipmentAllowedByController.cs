using PagedList;
using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using ZB_FEPMS.Action_Filters;
using ZB_FEPMS.Models;

namespace ZB_FEPMS.Controllers
{

    [RBAC]
    [NoCache]
    public class ShipmentAllowedByController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
        private int sizeOfPage = 15;
        int numberOfPage = 1;

        public ActionResult Index(int? page)
        {
            numberOfPage = (page ?? 1);
            var shipmentAllowedByList = db.tbl_lu_ShipmentAllowedBy.OrderBy(tlsab => tlsab.name);
            return View(shipmentAllowedByList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(tbl_lu_ShipmentAllowedBy shipmentAllowedBy)
        {
            if (string.IsNullOrEmpty(shipmentAllowedBy.name))
            {
                ModelState.AddModelError("name", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            tbl_lu_ShipmentAllowedBy _ShipmentAllowedBy = new tbl_lu_ShipmentAllowedBy();
                            _ShipmentAllowedBy.name = shipmentAllowedBy.name;
                            _ShipmentAllowedBy.description = shipmentAllowedBy.description;
                            dbe.tbl_lu_ShipmentAllowedBy.Add(_ShipmentAllowedBy);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "ShipmentAllowedBy-Create";
                            string object_id = _ShipmentAllowedBy.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            return RedirectToAction("Index");
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                        }
                    }
                }
            }
            return View(shipmentAllowedBy);
        }

        public ActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            tbl_lu_ShipmentAllowedBy shipmentAllowedBy = db.tbl_lu_ShipmentAllowedBy.Find(id);
            if (shipmentAllowedBy == null)
            {
                return HttpNotFound();
            }
            return View(shipmentAllowedBy);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(tbl_lu_ShipmentAllowedBy shipmentAllowedBy)
        {
            if (string.IsNullOrEmpty(shipmentAllowedBy.name))
            {
                ModelState.AddModelError("name", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            tbl_lu_ShipmentAllowedBy _ShipmentAllowedBy = dbe.tbl_lu_ShipmentAllowedBy.Find(shipmentAllowedBy.Id);
                            _ShipmentAllowedBy.name = shipmentAllowedBy.name;
                            _ShipmentAllowedBy.description = shipmentAllowedBy.description;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "ShipmentAllowedBy-Edit";
                            string object_id = _ShipmentAllowedBy.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            return RedirectToAction("Index");
                        }
                        catch (Exception)
                        {
                            dbeTransaction.Rollback();
                        }
                    }
                }
            }
            return View(shipmentAllowedBy);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
