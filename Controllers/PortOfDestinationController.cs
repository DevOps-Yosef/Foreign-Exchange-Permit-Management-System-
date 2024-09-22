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
    public class PortOfDestinationController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
        private int sizeOfPage = 15;
        int numberOfPage = 1;

        public ActionResult Index(int? page)
        {
            numberOfPage = (page ?? 1);
            var portOfDestinationList = db.tbl_lu_PortOfDestination.OrderBy(tlpod => tlpod.name);
            return View(portOfDestinationList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(tbl_lu_PortOfDestination portOfDestination)
        {
            if (string.IsNullOrEmpty(portOfDestination.name))
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
                            tbl_lu_PortOfDestination _PortOfDestination = new tbl_lu_PortOfDestination();
                            _PortOfDestination.name = portOfDestination.name;
                            _PortOfDestination.description = portOfDestination.description;
                            dbe.tbl_lu_PortOfDestination.Add(_PortOfDestination);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "PortOfDestination-Create";
                            string object_id = _PortOfDestination.Id.ToString();
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
            return View(portOfDestination);
        }

        public ActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            tbl_lu_PortOfDestination portOfDestination = db.tbl_lu_PortOfDestination.Find(id);
            if (portOfDestination == null)
            {
                return HttpNotFound();
            }
            return View(portOfDestination);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(tbl_lu_PortOfDestination portOfDestination)
        {
            if (string.IsNullOrEmpty(portOfDestination.name))
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
                            tbl_lu_PortOfDestination _PortOfDestination = dbe.tbl_lu_PortOfDestination.Find(portOfDestination.Id);
                            _PortOfDestination.name = portOfDestination.name;
                            _PortOfDestination.description = portOfDestination.description;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "PortOfDestination-Edit";
                            string object_id = _PortOfDestination.Id.ToString();
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
            return View(portOfDestination);
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
