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
    public class MethodOfPaymentController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
        private int sizeOfPage = 15;
        int numberOfPage = 1;

        public ActionResult Index(int? page)
        {
            numberOfPage = (page ?? 1);
            var methodOfPaymentList = db.tbl_lu_MethodOfPayment.OrderBy(tlmop => tlmop.name);
            return View(methodOfPaymentList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(tbl_lu_MethodOfPayment methodOfPayment)
        {
            if (string.IsNullOrEmpty(methodOfPayment.name))
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
                            tbl_lu_MethodOfPayment _MethodOfPayment = new tbl_lu_MethodOfPayment();
                            _MethodOfPayment.name = methodOfPayment.name;
                            _MethodOfPayment.description = methodOfPayment.description;
                            dbe.tbl_lu_MethodOfPayment.Add(_MethodOfPayment);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "MethodOfPayment-Create";
                            string object_id = _MethodOfPayment.Id.ToString();
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
            return View(methodOfPayment);
        }

        public ActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            tbl_lu_MethodOfPayment methodOfPayment = db.tbl_lu_MethodOfPayment.Find(id);
            if (methodOfPayment == null)
            {
                return HttpNotFound();
            }
            return View(methodOfPayment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(tbl_lu_MethodOfPayment methodOfPayment)
        {
            if (string.IsNullOrEmpty(methodOfPayment.name))
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
                            tbl_lu_MethodOfPayment _MethodOfPayment = dbe.tbl_lu_MethodOfPayment.Find(methodOfPayment.Id);
                            _MethodOfPayment.name = methodOfPayment.name;
                            _MethodOfPayment.description = methodOfPayment.description;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "MethodOfPayment-Edit";
                            string object_id = _MethodOfPayment.Id.ToString();
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
            return View(methodOfPayment);
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
