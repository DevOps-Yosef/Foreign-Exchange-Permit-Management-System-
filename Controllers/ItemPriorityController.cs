using PagedList;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using ZB_FEPMS.Action_Filters;
using ZB_FEPMS.Models;

namespace ZB_FEPMS.Controllers
{

    [RBAC]
    [NoCache]
    public class ItemPriorityController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
        private int sizeOfPage = 15;
        int numberOfPage = 1;

        public ActionResult Index(int? page)
        {
            numberOfPage = (page ?? 1);
            var itemPriorityList = db.tblItemPriorities.OrderBy(tip => tip.Priority)
                .ThenBy(tip => tip.GroupBy).ThenBy(tip => tip.Name);
            return View(itemPriorityList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public JsonResult GroupByByPriority(string itemPriority)
        {
            string result = "";
            try
            {
                if (!string.IsNullOrEmpty(itemPriority))
                {
                    db.Configuration.ProxyCreationEnabled = false;
                    var itemPriorityList = db.tblItemPriorities
                        .Where(tip => tip.Priority.Equals(itemPriority))
                        .GroupBy(tip => tip.GroupBy).ToList()
                         .Select(c => new NameValuePair()
                         {
                             Name = c.First().GroupBy,
                             Value = c.First().GroupBy
                         }).OrderBy(c => c.Name);
                    JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
                    result = javaScriptSerializer.Serialize(itemPriorityList);
                }
            }
            catch (Exception) { }
            return Json(result, JsonRequestBehavior.AllowGet);
        }
        public void initPriorityForm()
        {
            List<SelectListItem> priority = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "First Priority", Value = "First Priority"
                },
                new SelectListItem {
                    Text = "Second Priority", Value = "Second Priority"
                },
                new SelectListItem {
                    Text = "Third Priority", Value = "Third Priority"
                },
            };
            ViewBag.Priority = priority;
        }

        public ActionResult Create()
        {
            initPriorityForm();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(tblItemPriority itemPriority)
        {
            if (string.IsNullOrEmpty(itemPriority.Priority))
            {
                ModelState.AddModelError("Priority", "Required.");
            }
            if (itemPriority.is_new)
            {
                if (string.IsNullOrEmpty(itemPriority.GroupByValue))
                {
                    ModelState.AddModelError("GroupByValue", "Required.");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(itemPriority.GroupBy))
                {
                    ModelState.AddModelError("GroupBy", "Required.");
                }
            }
            if (string.IsNullOrEmpty(itemPriority.Name))
            {
                ModelState.AddModelError("Name", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            tblItemPriority _ItemPriority = new tblItemPriority();
                            _ItemPriority.Priority = itemPriority.Priority;
                            if (itemPriority.is_new)
                            {
                                _ItemPriority.GroupBy = itemPriority.GroupByValue;
                            }
                            else
                            {
                                _ItemPriority.GroupBy = itemPriority.GroupBy;
                            }
                            _ItemPriority.Name = itemPriority.Name;
                            dbe.tblItemPriorities.Add(_ItemPriority);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "ItemPriority-Create";
                            string object_id = _ItemPriority.Id.ToString();
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
            initPriorityForm();
            return View(itemPriority);
        }

        public ActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            tblItemPriority itemPriority = db.tblItemPriorities.Find(id);
            if (itemPriority == null)
            {
                return HttpNotFound();
            }
            return View(itemPriority);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(tblItemPriority itemPriority)
        {
            if (string.IsNullOrEmpty(itemPriority.Name))
            {
                ModelState.AddModelError("Name", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            tblItemPriority _ItemPriority = dbe.tblItemPriorities.Find(itemPriority.Id);
                            _ItemPriority.Name = itemPriority.Name;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "ItemPriority-Edit";
                            string object_id = _ItemPriority.Id.ToString();
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
            return View(itemPriority);
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
