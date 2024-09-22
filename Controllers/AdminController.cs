using PagedList;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Routing;
using ZB_FEPMS.Action_Filters;
using ZB_FEPMS.Models;

namespace ZB_FEPMS.Controllers
{
    [RBAC]
    [NoCache]
    public class AdminController : Controller
    {
        private ZB_FEPMS_Model database = new ZB_FEPMS_Model();
        private int sizeOfPage = 15;
        private int numberOfPage = 1;

        #region USERS
        // GET: Admin

        [HttpGet]
        public ActionResult Index(string name, int? page)
        {
            numberOfPage = (page ?? 1);
            if (!string.IsNullOrEmpty(name))
            {
                page = 1;
                var userList = database.USERS
                    .Where(u => (u.Firstname.Trim().Contains(name) || u.Lastname.Trim().Contains(name)))
                    .OrderBy(u => u.Firstname).ThenBy(u => u.Lastname);
                ViewBag.tagNumber = name;
                return View(userList.ToPagedList(numberOfPage, sizeOfPage));
            }
            else
            {
                var userList = database.USERS
                    .OrderBy(u => u.Firstname)
                    .ThenBy(u => u.Lastname);
                return View(userList.ToPagedList(numberOfPage, sizeOfPage));
            }
        }

        [HttpGet]
        public ActionResult AccessLogList(DateTime? from, DateTime? to, string name, string ip, string machineName, int? page)
        {
            numberOfPage = (page ?? 1);
            var accessLogList = database.AccessLogs.Where(al => al.id != null);
            if (from.HasValue && to.HasValue)
            {
                page = 1;
                accessLogList = accessLogList.Where(al => al.date >= from && al.date <= to);
                ViewBag.from = from;
                ViewBag.to = to;
            }
            if (!string.IsNullOrEmpty(name))
            {
                page = 1;
                accessLogList = accessLogList.Where(al => al.user_full_name.Contains(name));
                ViewBag.name = name;
            }
            if (!string.IsNullOrEmpty(ip))
            {
                page = 1;
                accessLogList = accessLogList.Where(al => al.ip_address.Contains(ip));
                ViewBag.ip = ip;
            }
            if (!string.IsNullOrEmpty(machineName))
            {
                page = 1;
                accessLogList = accessLogList.Where(al => al.machine_name.Contains(machineName));
                ViewBag.machineName = machineName;
            }
            accessLogList = accessLogList.OrderByDescending(al => al.date).ThenBy(al => al.user_full_name);
            return View(accessLogList.ToPagedList(numberOfPage, sizeOfPage));
        }

        [HttpGet]
        public PartialViewResult filter4Users(string _surname)
        {
            return PartialView("_ListUserTable", GetFilteredUserList(_surname));
        }

        public ViewResult UserDetails(Guid id)
        {
            USER user = database.USERS.Find(id);
            SetViewBagData(id);
            return View(user);
        }

        [HttpGet]
        public ActionResult UserCreate()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UserCreate([Bind(Include = "Username,Firstname,Lastname,IDNo,Title,Initial,EMail")] USER user)
        {
            if (string.IsNullOrEmpty(user.Username))
            {
                ModelState.AddModelError("Username", "Required.");
            }
            if (string.IsNullOrEmpty(user.Firstname))
            {
                ModelState.AddModelError("Firstname", "Required.");
            }
            if (string.IsNullOrEmpty(user.Lastname))
            {
                ModelState.AddModelError("Lastname", "Required.");
            }
            if (string.IsNullOrEmpty(user.IDNo))
            {
                ModelState.AddModelError("IDNo", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            //Encrypt_Decrypt _encryptDecryptObj = new Encrypt_Decrypt();
                            List<string> results = dbe.Database.SqlQuery<String>(string.Format("SELECT Username FROM USERS WHERE Username = '{0}'", user.Username)).ToList();
                            bool _userExistsInTable = (results.Count > 0);
                            USER _user = null;
                            if (_userExistsInTable)
                            {
                                ModelState.AddModelError(string.Empty, "USER already exists!");
                            }
                            else
                            {
                                _user = new USER();
                                _user.Username = user.Username;
                                _user.Lastname = user.Lastname;
                                _user.Firstname = user.Firstname;
                                _user.IDNo = user.IDNo;
                                _user.Title = user.Title;
                                _user.Initial = user.Initial;
                                _user.EMail = user.EMail;
                                //_user.password = _encryptDecryptObj.Encrypt("password");
                                _user.Inactive = false;
                                _user.LastModified = System.DateTime.Now;
                                dbe.USERS.Add(_user);
                                dbe.SaveChanges();
                                RBACUser rbacUserObj = new RBACUser();
                                string operation = "Admin-UserCreate";
                                string object_id = _user.UserId.ToString();
                                rbacUserObj.saveActivityLog(dbe, operation, object_id);
                                dbeTransaction.Commit();
                                return RedirectToAction("Index", "Admin");
                            }
                        }
                        catch (Exception)
                        {
                            dbeTransaction.Rollback();
                        }
                    }
                }
            }
            return View(user);
        }

        [HttpGet]
        public ActionResult UserEdit(Guid id)
        {
            USER user = database.USERS.Find(id);
            user.Username = user.Username.Trim();
            SetViewBagData(id);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UserEdit(USER user)
        {
            using (var dbe = new ZB_FEPMS_Model())
            {
                using (var dbeTransaction = dbe.Database.BeginTransaction())
                {
                    try
                    {
                        USER _user = dbe.USERS.Find(user.UserId);
                        if (string.IsNullOrEmpty(user.Firstname))
                        {
                            ModelState.AddModelError("Firstname", "Required.");
                        }
                        if (string.IsNullOrEmpty(user.Lastname))
                        {
                            ModelState.AddModelError("Lastname", "Required.");
                        }
                        if (string.IsNullOrEmpty(user.IDNo))
                        {
                            ModelState.AddModelError("IDNo", "Required.");
                        }
                        if (_user != null && ModelState.IsValid)
                        {
                            _user.Lastname = user.Lastname;
                            _user.Firstname = user.Firstname;
                            _user.IDNo = user.IDNo;
                            _user.Title = user.Title;
                            _user.Initial = user.Initial;
                            _user.EMail = user.EMail;
                            _user.Inactive = user.Inactive;
                            _user.LastModified = DateTime.Now;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Admin-UserEdit";
                            string object_id = _user.UserId.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            return RedirectToAction("UserDetails", new RouteValueDictionary(new { id = user.UserId }));
                        }
                    }
                    catch (Exception)
                    {
                        dbeTransaction.Rollback();
                    }
                }
            }
            SetViewBagData(user.UserId);
            return View(user);
        }

        [HttpPost]
        public ActionResult UserDetails(USER user)
        {
            if (user.Username == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid USER Name");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            USER userObj = dbe.USERS.Find(user.UserId);
                            userObj.Inactive = user.Inactive;
                            userObj.LastModified = System.DateTime.Now;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Admin-UserDetails";
                            string object_id = user.UserId.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                        }
                        catch (Exception)
                        {
                            dbeTransaction.Rollback();
                        }
                    }
                }
            }
            return View(user);
        }

        [HttpGet]
        public ActionResult DeleteUserRole(Guid id, Guid userId)
        {
            ROLE role = database.ROLES.Find(id);
            USER user = database.USERS.Find(userId);

            if (role.USERS.Contains(user))
            {
                role.USERS.Remove(user);
                database.SaveChanges();
            }
            return RedirectToAction("Details", "USER", new { id = userId });
        }

        [HttpGet]
        public PartialViewResult filterReset()
        {
            return PartialView("_ListUserTable", database.USERS
                .Where(r => r.Inactive == false || r.Inactive == null)
                .OrderBy(r => r.Firstname).ThenBy(r => r.Lastname).ToList());
        }

        [HttpGet]
        public ActionResult DeleteUserReturnPartialView(Guid userId)
        {
            using (var dbe = new ZB_FEPMS_Model())
            {
                using (var dbeTransaction = dbe.Database.BeginTransaction())
                {
                    try
                    {
                        USER user = dbe.USERS.Find(userId);
                        if (user != null)
                        {
                            dbe.Entry(user).Entity.Inactive = true;
                            dbe.Entry(user).Entity.UserId = user.UserId;
                            dbe.Entry(user).Entity.LastModified = System.DateTime.Now;
                            dbe.Entry(user).State = EntityState.Modified;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Admin-DeleteUserReturnPartialView";
                            string object_id = user.UserId.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                        }
                    }
                    catch
                    {
                        dbeTransaction.Rollback();
                    }
                }
            }
            return RedirectToAction("Index");
        }

        private IEnumerable<USER> GetFilteredUserList(string _surname)
        {
            IEnumerable<USER> _ret = null;
            try
            {
                if (string.IsNullOrEmpty(_surname))
                {
                    _ret = database.USERS.Where(r => r.Inactive == false || r.Inactive == null)
                        .OrderBy(r => r.Firstname).ThenBy(r => r.Lastname).ToList();
                }
                else
                {
                    _ret = database.USERS.Where(p => p.Lastname == _surname).OrderBy(r => r.Firstname)
                        .ThenBy(r => r.Lastname).ToList();
                }
            }
            catch
            {
            }
            return _ret;
        }

        protected override void Dispose(bool disposing)
        {
            database.Dispose();
            base.Dispose(disposing);
        }

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult DeleteUserRoleReturnPartialView(Guid id, Guid userId)
        {
            using (var dbe = new ZB_FEPMS_Model())
            {
                using (var dbeTransaction = dbe.Database.BeginTransaction())
                {
                    try
                    {
                        ROLE role = dbe.ROLES.Find(id);
                        USER user = dbe.USERS.Find(userId);
                        if (role.USERS.Contains(user))
                        {
                            role.USERS.Remove(user);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Admin-DeleteUserRoleReturnPartialView";
                            string object_id = user.UserId.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                        }
                    }
                    catch (Exception)
                    {
                        dbeTransaction.Rollback();
                    }
                }
            }
            //SetViewBagData(userId);
            //return PartialView("_ListUserRoleTable", database.USERS.Find(userId));
            return RedirectToAction("UserEdit", "Admin", new { id = userId });
        }

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public PartialViewResult AddUserRoleReturnPartialView(Guid id, Guid userId)
        {
            using (var dbe = new ZB_FEPMS_Model())
            {
                using (var dbeTransaction = dbe.Database.BeginTransaction())
                {
                    try
                    {
                        ROLE role = dbe.ROLES.Find(id);
                        USER user = dbe.USERS.Find(userId);
                        if (!role.USERS.Contains(user))
                        {
                            role.USERS.Add(user);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Admin-AddUserRoleReturnPartialView";
                            string object_id = user.UserId.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                        }
                    }
                    catch (Exception)
                    {
                        dbeTransaction.Rollback();
                    }
                }
            }
            SetViewBagData(userId);
            return PartialView("_ListUserRoleTable", database.USERS.Find(userId));
        }

        private void SetViewBagData(Guid _userId)
        {
            ViewBag.UserId = _userId;
            ViewBag.List_boolNullYesNo = this.List_boolNullYesNo();
            ViewBag.RoleId = new SelectList(database.ROLES.OrderBy(p => p.RoleName), "RoleId", "RoleName");
        }

        public List<SelectListItem> List_boolNullYesNo()
        {
            var _retVal = new List<SelectListItem>();
            try
            {
                _retVal.Add(new SelectListItem { Text = "Not Set", Value = null });
                _retVal.Add(new SelectListItem { Text = "Yes", Value = bool.TrueString });
                _retVal.Add(new SelectListItem { Text = "No", Value = bool.FalseString });
            }
            catch { }
            return _retVal;
        }
        #endregion

        #region ROLES
        public ActionResult RoleIndex(int? page)
        {
            numberOfPage = (page ?? 1);
            var roleList = database.ROLES.OrderBy(r => r.RoleName);
            return View(roleList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ViewResult RoleDetails(Guid id)
        {
            USER user = database.USERS.Where(r => r.Username == User.Identity.Name).FirstOrDefault();
            ROLE role = database.ROLES.Where(r => r.RoleId.Equals(id))
                   .Include(a => a.PERMISSIONS)
                   .Include(a => a.USERS)
                   .FirstOrDefault();

            // USERS combo
            ViewBag.UserId = new SelectList(database.USERS.Where(r => r.Inactive == false || r.Inactive == null), "Id", "Username");
            ViewBag.RoleId = id;

            // Rights combo
            ViewBag.PermissionId = new SelectList(database.PERMISSIONS.OrderBy(a => a.PermissionDescription), "PermissionId", "PermissionDescription");
            ViewBag.List_boolNullYesNo = this.List_boolNullYesNo();

            return View(role);
        }

        public ActionResult RoleCreate()
        {
            USER user = database.USERS
                .FirstOrDefault(r => r.Username == User.Identity.Name);
            ViewBag.List_boolNullYesNo = this.List_boolNullYesNo();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RoleCreate(ROLE _role)
        {
            //DateTime _currentDate = new DateTime.no();
            if (string.IsNullOrEmpty(_role.RoleName))
            {
                ModelState.AddModelError("RoleName", "Required");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            ROLE roleObj = new ROLE();
                            roleObj.RoleName = _role.RoleName;
                            roleObj.RoleDescription = _role.RoleDescription;
                            roleObj.LastModified = DateTime.Now;
                            dbe.ROLES.Add(roleObj);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Admin-RoleCreate";
                            string object_id = roleObj.RoleId.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            return RedirectToAction("RoleIndex");
                        }
                        catch (Exception)
                        {
                            dbeTransaction.Rollback();
                        }
                    }
                }
            }
            ViewBag.List_boolNullYesNo = this.List_boolNullYesNo();
            return View(_role);
        }


        public ActionResult RoleEdit(Guid id)
        {
            USER user = database.USERS
                .Where(r => r.Username == User.Identity.Name).FirstOrDefault();
            ROLE _role = database.ROLES.Where(r => r.RoleId.Equals(id))
                    .Include(a => a.PERMISSIONS)
                    .Include(a => a.USERS)
                    .FirstOrDefault();
            // USERS combo
            ViewBag.UserId = new SelectList(database.USERS
                .Where(r => r.Inactive == false || r.Inactive == null), "UserId", "Username");
            ViewBag.RoleId = id;
            // Rights combo
            ViewBag.PermissionId = new SelectList(database.PERMISSIONS
                .OrderBy(p => p.PermissionDescription), "PermissionId", "PermissionDescription");
            ViewBag.List_boolNullYesNo = this.List_boolNullYesNo();
            return View(_role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RoleEdit(ROLE _role)
        {
            if (string.IsNullOrEmpty(_role.RoleName))
            {
                ModelState.AddModelError("RoleName", "Required");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            ROLE roleObj = dbe.ROLES.Find(_role.RoleId);
                            roleObj.RoleName = _role.RoleName;
                            roleObj.RoleDescription = _role.RoleDescription;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Admin-RoleEdit";
                            string object_id = roleObj.RoleId.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            return RedirectToAction("RoleIndex");
                        }
                        catch (Exception)
                        {
                            dbeTransaction.Rollback();
                        }
                    }
                }
            }
            // USERS combo
            ViewBag.UserId = new SelectList(database.USERS.Where(r => r.Inactive == false || r.Inactive == null), "UserId", "Username");
            // Rights combo
            ViewBag.PermissionId = new SelectList(database.PERMISSIONS.OrderBy(a => a.PermissionId), "PermissionId", "PermissionDescription");
            ViewBag.List_boolNullYesNo = this.List_boolNullYesNo();
            return View(_role);
        }


        public ActionResult RoleDelete(Guid id)
        {
            ROLE _role = database.ROLES.Find(id);
            if (_role != null)
            {
                _role.USERS.Clear();
                _role.PERMISSIONS.Clear();

                database.Entry(_role).State = EntityState.Deleted;
                database.SaveChanges();
            }
            return RedirectToAction("RoleIndex");
        }

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public PartialViewResult DeleteUserFromRoleReturnPartialView(Guid id, Guid userId)
        {
            ROLE role = database.ROLES.Find(id);
            USER user = database.USERS.Find(userId);

            if (role.USERS.Contains(user))
            {
                role.USERS.Remove(user);
                database.SaveChanges();
            }
            return PartialView("_ListUsersTable4Role", role);
        }

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public PartialViewResult AddUser2RoleReturnPartialView(Guid id, Guid userId)
        {
            ROLE role = database.ROLES.Find(id);
            USER user = database.USERS.Find(userId);

            if (!role.USERS.Contains(user))
            {
                role.USERS.Add(user);
                database.SaveChanges();
            }
            return PartialView("_ListUsersTable4Role", role);
        }

        #endregion

        #region PERMISSIONS

        public ViewResult PermissionIndex(int? page)
        {
            numberOfPage = (page ?? 1);
            var permissionList = database.PERMISSIONS.OrderBy(p => p.PermissionDescription);
            return View(permissionList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ViewResult PermissionDetails(Guid id)
        {
            PERMISSION _permission = database.PERMISSIONS.Find(id);
            return View(_permission);
        }

        public ActionResult PermissionCreate()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PermissionCreate(PERMISSION _permission)
        {
            if (_permission.PermissionDescription == null)
            {
                ModelState.AddModelError("Permission Description", "Permission Description must be entered");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            dbe.PERMISSIONS.Add(_permission);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Admin-PermissionCreate";
                            string object_id = _permission.PermissionId.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            return RedirectToAction("PermissionIndex");
                        }
                        catch (Exception)
                        {
                            dbeTransaction.Rollback();
                        }
                    }
                }
            }
            return View(_permission);
        }

        public ActionResult PermissionEdit(Guid id)
        {
            PERMISSION _permission = database.PERMISSIONS.Find(id);
            ViewBag.RoleId = new SelectList(database.ROLES.OrderBy(p => p.RoleDescription), "RoleId", "RoleDescription");
            return View(_permission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PermissionEdit(PERMISSION _permission)
        {
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            PERMISSION permissionObj = dbe.PERMISSIONS.Find(_permission.PermissionId);
                            permissionObj.PermissionDescription = _permission.PermissionDescription;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Admin-PermissionEdit";
                            string object_id = _permission.PermissionId.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            return RedirectToAction("PermissionIndex");
                        }
                        catch (Exception)
                        {
                            dbeTransaction.Rollback();
                        }
                    }
                }
            }
            return View(_permission);
        }

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult PermissionDelete(Guid id)
        {
            PERMISSION permission = database.PERMISSIONS.Find(id);
            if (permission.ROLES.Count > 0)
                permission.ROLES.Clear();

            database.Entry(permission).State = EntityState.Deleted;
            database.SaveChanges();
            return RedirectToAction("PermissionIndex");
        }

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public PartialViewResult AddPermission2RoleReturnPartialView(Guid id, Guid permissionId)
        {
            ROLE role = database.ROLES.Find(id);
            PERMISSION _permission = database.PERMISSIONS.Find(permissionId);

            if (!role.PERMISSIONS.Contains(_permission))
            {
                role.PERMISSIONS.Add(_permission);
                database.SaveChanges();
            }
            return PartialView("_ListPermissions", role);
        }

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public PartialViewResult AddAllPermissions2RoleReturnPartialView(Guid id)
        {
            ROLE _role = database.ROLES.Where(p => p.RoleId.Equals(id)).FirstOrDefault();
            List<PERMISSION> _permissions = database.PERMISSIONS.ToList();
            foreach (PERMISSION _permission in _permissions)
            {
                if (!_role.PERMISSIONS.Contains(_permission))
                {
                    _role.PERMISSIONS.Add(_permission);

                }
            }
            database.SaveChanges();
            return PartialView("_ListPermissions", _role);
        }

        //[HttpGet]
        //[OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult DeletePermissionFromRoleReturnPartialView(Guid id, Guid permissionId)
        {
            ROLE _role = database.ROLES.Find(id);
            PERMISSION _permission = database.PERMISSIONS.Find(permissionId);

            if (_role.PERMISSIONS.Contains(_permission))
            {
                _role.PERMISSIONS.Remove(_permission);
                database.SaveChanges();
            }

            return RedirectToAction("RoleEdit", new RouteValueDictionary(new { id = id }));
            //return PartialView("_ListPermissions", _role);
        }

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public PartialViewResult DeleteRoleFromPermissionReturnPartialView(Guid id, Guid permissionId)
        {
            ROLE role = database.ROLES.Find(id);
            PERMISSION permission = database.PERMISSIONS.Find(permissionId);

            if (role.PERMISSIONS.Contains(permission))
            {
                role.PERMISSIONS.Remove(permission);
                database.SaveChanges();
            }
            return PartialView("_ListRolesTable4Permission", permission);
        }

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public PartialViewResult AddRole2PermissionReturnPartialView(Guid permissionId, Guid roleId)
        {
            ROLE role = database.ROLES.Find(roleId);
            PERMISSION _permission = database.PERMISSIONS.Find(permissionId);

            if (!role.PERMISSIONS.Contains(_permission))
            {
                role.PERMISSIONS.Add(_permission);
                database.SaveChanges();
            }
            return PartialView("_ListRolesTable4Permission", _permission);
        }

        public ActionResult PermissionsImport()
        {
            var _controllerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t != null
                    && t.IsPublic
                    && t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                    && !t.IsAbstract
                    && typeof(IController).IsAssignableFrom(t));

            var _controllerMethods = _controllerTypes.ToDictionary(controllerType => controllerType,
                    controllerType => controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => typeof(ActionResult).IsAssignableFrom(m.ReturnType)));

            foreach (var _controller in _controllerMethods)
            {
                string _controllerName = _controller.Key.Name;
                foreach (var _controllerAction in _controller.Value)
                {
                    string _controllerActionName = _controllerAction.Name;
                    if (_controllerName.EndsWith("Controller"))
                    {
                        _controllerName = _controllerName.Substring(0, _controllerName.LastIndexOf("Controller"));
                    }

                    string _permissionDescription = string.Format("{0}-{1}", _controllerName, _controllerActionName);
                    PERMISSION _permission = database.PERMISSIONS.Where(p => p.PermissionDescription == _permissionDescription).FirstOrDefault();
                    if (_permission == null)
                    {
                        if (ModelState.IsValid)
                        {
                            PERMISSION _perm = new PERMISSION();
                            _perm.PermissionDescription = _permissionDescription;

                            database.PERMISSIONS.Add(_perm);
                            database.SaveChanges();
                        }
                    }
                }
            }
            return RedirectToAction("PermissionIndex");
        }
        #endregion
    }
}