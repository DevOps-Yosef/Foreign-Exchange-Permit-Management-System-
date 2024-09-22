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
    public class StatusController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
        private int sizeOfPage = 15;
        int numberOfPage = 1;

        public ActionResult Index(int? page)
        {
            numberOfPage = (page ?? 1);
            var statusList = db.tbl_lu_Status.OrderBy(tls => tls.name);
            return View(statusList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(tbl_lu_Status status)
        {
            if (string.IsNullOrEmpty(status.name))
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
                            tbl_lu_Status _Status = new tbl_lu_Status();
                            _Status.name = status.name;
                            _Status.description = status.description;
                            dbe.tbl_lu_Status.Add(_Status);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Status-Create";
                            string object_id = _Status.Id.ToString();
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
            return View(status);
        }

        public ActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            tbl_lu_Status status = db.tbl_lu_Status.Find(id);
            if (status == null)
            {
                return HttpNotFound();
            }
            return View(status);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(tbl_lu_Status status)
        {
            if (string.IsNullOrEmpty(status.name))
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
                            tbl_lu_Status _Status = dbe.tbl_lu_Status.Find(status.Id);
                            _Status.name = status.name;
                            _Status.description = status.description;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Status-Edit";
                            string object_id = _Status.Id.ToString();
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
            return View(status);
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
