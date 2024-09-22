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
    public class PortOfLoadingController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
        private int sizeOfPage = 15;
        int numberOfPage = 1;

        public ActionResult Index(int? page)
        {
            numberOfPage = (page ?? 1);
            var portOfLoadingList = db.tbl_lu_PortOfLoading.OrderBy(tlpol => tlpol.name);
            return View(portOfLoadingList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(tbl_lu_PortOfLoading portOfLoading)
        {
            if (string.IsNullOrEmpty(portOfLoading.name))
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
                            tbl_lu_PortOfLoading _PortOfLoading = new tbl_lu_PortOfLoading();
                            _PortOfLoading.name = portOfLoading.name;
                            _PortOfLoading.description = portOfLoading.description;
                            dbe.tbl_lu_PortOfLoading.Add(_PortOfLoading);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "PortOfLoading-Create";
                            string object_id = _PortOfLoading.Id.ToString();
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
            return View(portOfLoading);
        }

        public ActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            tbl_lu_PortOfLoading portOfLoading = db.tbl_lu_PortOfLoading.Find(id);
            if (portOfLoading == null)
            {
                return HttpNotFound();
            }
            return View(portOfLoading);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(tbl_lu_PortOfLoading portOfLoading)
        {
            if (string.IsNullOrEmpty(portOfLoading.name))
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
                            tbl_lu_PortOfLoading _PortOfLoading = dbe.tbl_lu_PortOfLoading.Find(portOfLoading.Id);
                            _PortOfLoading.name = portOfLoading.name;
                            _PortOfLoading.description = portOfLoading.description;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "PortOfLoading-Edit";
                            string object_id = _PortOfLoading.Id.ToString();
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
            return View(portOfLoading);
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
