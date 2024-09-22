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
    public class CountryOfOriginController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
        private int sizeOfPage = 15;
        int numberOfPage = 1;

        public ActionResult Index(int? page)
        {
            numberOfPage = (page ?? 1);
            var countryOfOriginList = db.tbl_lu_CountryOfOrigin.OrderBy(tlcoo => tlcoo.name);
            return View(countryOfOriginList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(tbl_lu_CountryOfOrigin countryOfOrigin)
        {
            if (string.IsNullOrEmpty(countryOfOrigin.name))
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
                            tbl_lu_CountryOfOrigin _CountryOfOrigin = new tbl_lu_CountryOfOrigin();
                            _CountryOfOrigin.name = countryOfOrigin.name;
                            _CountryOfOrigin.description = countryOfOrigin.description;
                            dbe.tbl_lu_CountryOfOrigin.Add(_CountryOfOrigin);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "CountryOfOrigin-Create";
                            string object_id = _CountryOfOrigin.Id.ToString();
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
            return View(countryOfOrigin);
        }

        public ActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            tbl_lu_CountryOfOrigin countryOfOrigin = db.tbl_lu_CountryOfOrigin.Find(id);
            if (countryOfOrigin == null)
            {
                return HttpNotFound();
            }
            return View(countryOfOrigin);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(tbl_lu_CountryOfOrigin countryOfOrigin)
        {
            if (string.IsNullOrEmpty(countryOfOrigin.name))
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
                            tbl_lu_CountryOfOrigin _CountryOfOrigin = dbe.tbl_lu_CountryOfOrigin.Find(countryOfOrigin.Id);
                            _CountryOfOrigin.name = countryOfOrigin.name;
                            _CountryOfOrigin.description = countryOfOrigin.description;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "CountryOfOrigin-Edit";
                            string object_id = _CountryOfOrigin.Id.ToString();
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
            return View(countryOfOrigin);
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
