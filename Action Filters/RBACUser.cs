using ZB_FEPMS.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ZB_FEPMS.Action_Filters;
//using System.Data.Objects.SqlClient;
using System.Reflection;
using System.Net;
using System.Net.Mail;

public class RBACUser
{
    private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
    public RBACUser() { }
    public Guid UserId { get; set; }
    public bool IsSysAdmin { get; set; }
    public string Username { get; set; }
    private List<UserRole> Roles = new List<UserRole>();
    DateTime currentDate = DateTime.Today.Date;

    public RBACUser(string _username)
    {
        this.Username = _username;
        this.IsSysAdmin = false;
        GetDatabaseUserRolesPermissions();
    }

    public string getFullNameOfUserByUsername(string _userName)
    {
        string fullName = "";
        if (!string.IsNullOrEmpty(_userName))
        {
            using (ZB_FEPMS_Model _data = new ZB_FEPMS_Model())
            {
                var _value = _data.USERS.Where(u => u.Username == _userName
                && u.Inactive == false).FirstOrDefault();
                if (_value != null)
                {
                    fullName = _value.Firstname + " " + _value.Lastname;
                }
            }
        }
        return fullName;
    }
    public string getFullNameOfUserById(string _userId)
    {
        string fullName = "";
        if (!string.IsNullOrEmpty(_userId))
        {
            Guid user_id = Guid.Parse(_userId);
            using (ZB_FEPMS_Model _data = new ZB_FEPMS_Model())
            {
                var _value = _data.USERS.Where(u => u.UserId.Equals(user_id)
                && u.Inactive == false).FirstOrDefault();
                if (_value != null)
                {
                    fullName = _value.Firstname + " " + _value.Lastname;
                }
            }
        }
        return fullName;
    }

    public string getFullNameOfUserById(ZB_FEPMS_Model dbe, string _userId)
    {
        string fullName = "";
        if (!string.IsNullOrEmpty(_userId))
        {
            Guid user_id = Guid.Parse(_userId);
            var _value = dbe.USERS.Find(user_id);
            if (_value != null)
            {
                fullName = _value.Firstname + " " + _value.Lastname;
            }
        }
        return fullName;
    }
    public void saveActivityLog(ZB_FEPMS_Model dbe, string operation, string object_id)
    {
        AccessLog accessLog = new AccessLog();
        accessLog.date = DateTime.Now;
        string fullName = HttpContext.Current.Session["fullNameAttribute"].ToString();
        var user_id = HttpContext.Current.Session["userIdAttribute"];
        if (user_id != null)
        {
            accessLog.user_full_name = fullName;
        }
        accessLog.user_full_name = fullName != null ? fullName.ToString() : "";
        accessLog.ip_address = System.Web.HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
        try
        {
            string machine_name = Dns.GetHostEntry(System.Web.HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"]).HostName;
            accessLog.machine_name = machine_name;
        }
        catch (Exception)
        { }
        accessLog.browser_info = HttpContext.Current.Request.ServerVariables["HTTP_URL"]
            + ":::::::::" + HttpContext.Current.Request.ServerVariables["HTTP_USER_AGENT"];
        accessLog.operation = operation;
        accessLog.object_info = object_id;
        dbe.AccessLogs.Add(accessLog);
        dbe.SaveChanges();
    }
    public void sendEmail(List<string> mailAddressList, string messageBody, string url)
    {
        try
        {
            string URI = string.IsNullOrEmpty(url) ? "" : url;
            string messageFooter = "<br/>You Can Login To the System Here https://aps3.zemenbank.com/FEPMS/"
                + URI
                + "<br/>Use Your Domain User Name and Password to Login. <br/><br/> Best Regards";
            string from = "ZB-FEPMS@zemenbank.com";
            string testMailAddress = "yosef.girma@zemenbank.com#Yosef";
            List<string> mailAddresses = new List<string>();
            mailAddresses.AddRange(mailAddressList.ToList());
            mailAddresses.Add(testMailAddress);
            foreach (string mailAddress in mailAddresses)
            {
                using (MailMessage mail = new MailMessage())
                {
                    mail.From = new MailAddress(from, "Zemen Bank FEPMS");
                    string messageHeader = "<span style=\"font-weight: bold; text-decoration: underline \">Dear " 
                        + mailAddress.Split('#')[1] + "</span> " + " <br/><br/>";
                    string fullMessage = messageHeader + messageBody + messageFooter;
                    mail.To.Add(new MailAddress(mailAddress.Split('#')[0]));
                    mail.Subject = "ZB-FEPMS Notification";
                    mail.Body = fullMessage;
                    mail.IsBodyHtml = true;
                    SmtpClient smtp = new SmtpClient();
                    smtp.UseDefaultCredentials = true;
                    smtp.Host = "smtp.zemenbank.com";
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.Send(mail);
                }
            }
        }
        catch (Exception exc)
        { }
    }

    public void saveErrorLog(ZB_FEPMS_Model dbe, string operation, int object_id, string error, string stackTrace)
    {
        AccessLog accessLog = new AccessLog();
        accessLog.date = DateTime.Now;
        string fullName = HttpContext.Current.Session["fullNameAttribute"].ToString();
        var user_id = HttpContext.Current.Session["userIdAttribute"];
        if (user_id != null)
        {
            accessLog.user_full_name = fullName;
        }
        accessLog.user_full_name = fullName != null ? fullName.ToString() : "";
        accessLog.ip_address = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
        try
        {
            string machine_name = Dns.GetHostEntry(HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"]).HostName;
            accessLog.machine_name = machine_name;
        }
        catch (Exception)
        { }
        accessLog.browser_info = HttpContext.Current.Request.ServerVariables["HTTP_URL"]
            + " ========= " + error + " ========= " + stackTrace;
        accessLog.operation = operation;
        accessLog.object_info = object_id.ToString();
        dbe.AccessLogs.Add(accessLog);
        dbe.SaveChanges();
    }
    private void GetDatabaseUserRolesPermissions()
    {
        try
        {
            using (ZB_FEPMS_Model _data = new ZB_FEPMS_Model())
            {
                USER _user = _data.USERS.FirstOrDefault(u => u.Username.Equals(this.Username)
                && u.Inactive == false);
                if (_user != null)
                {
                    this.UserId = _user.UserId;
                    foreach (ROLE _role in _user.ROLES)
                    {
                        UserRole _userRole = new UserRole { RoleId = _role.RoleId, RoleName = _role.RoleName };
                        foreach (PERMISSION _permission in _role.PERMISSIONS)
                        {
                            _userRole.Permissions.Add(new RolePermission { PermissionId = _permission.PermissionId, PermissionDescription = _permission.PermissionDescription });
                        }
                        this.Roles.Add(_userRole);

                        //if (!this.IsSysAdmin)
                        //    this.IsSysAdmin = (bool)_role.IsSysAdmin;
                    }
                }
            }
        }
        catch (Exception _ex)
        {
            if (_ex is System.Reflection.ReflectionTypeLoadException)
            {
                var typeLoadException = _ex as ReflectionTypeLoadException;
                var loaderExceptions = typeLoadException.LoaderExceptions;
            }
        }

    }
    public bool HasPermission(string requiredPermission)
    {
        bool bFound = false;
        foreach (UserRole role in this.Roles)
        {
            bFound = (role.Permissions.Where(p => p.PermissionDescription.ToLower() == requiredPermission.ToLower()).ToList().Count > 0);
            if (bFound)
                break;
        }
        return bFound;
    }
    public bool HasRole(string role)
    {
        return (Roles.Where(p => p.RoleName == role).ToList().Count > 0);
    }
    public string getRoleOfUserById(string _userId)
    {
        string roleName = "";
        if (!string.IsNullOrEmpty(_userId))
        {
            Guid user_id = Guid.Parse(_userId);
            using (ZB_FEPMS_Model _data = new ZB_FEPMS_Model())
            {
                var _value = _data.USERS.Where(u => u.UserId.Equals(user_id)
                && u.Inactive == false).FirstOrDefault();
                if (_value != null)
                {
                    var role = _value.ROLES.FirstOrDefault();
                    roleName = role.RoleName;
                }
            }
        }
        return roleName;
    }
    public string getRoleOfCurrentUser()
    {
        string roleName = "";
        string userId = System.Web.HttpContext.Current.Session["userIdAttribute"].ToString();
        if (!string.IsNullOrEmpty(userId))
        {
            Guid user_id = Guid.Parse(userId);
            using (ZB_FEPMS_Model _data = new ZB_FEPMS_Model())
            {
                var _value = _data.USERS.Where(u => u.UserId.Equals(user_id)
                && u.Inactive == false).FirstOrDefault();
                if (_value != null)
                {
                    var role = _value.ROLES.FirstOrDefault();
                    roleName = role.RoleName;
                }
            }
        }
        return roleName;
    }
    public bool isUserValid(String userName, String password)
    {
        bool isFound = false;
        //Encrypt_Decrypt _ecryptionObj = new Encrypt_Decrypt();
        try
        {

            using (ZB_FEPMS_Model _data = new ZB_FEPMS_Model())
            {

                //var user = _data.Database.SqlQuery<String>("select Username from [ZB-Inventory].[dbo].[USERS] where [Username]='"
                //    + userName + "'" + " and [password]='" + password.GetHashCode().ToString()+"'").ToList();

                //String _encryptedPassword = _ecryptionObj.Encrypt(password);
                //var user = _data.USERS.Where(r => r.Username == userName & r.password == _encryptedPassword).FirstOrDefault();
                //var user = _data.USERS.Where(r => r.Username == userName & r.password == password).FirstOrDefault();
                //var user = _data.Database.SqlQuery<String>("select Username from [ZB-Inventory].[dbo].[USERS] where [Username]='"
                //  + userName + "'").ToList();

                //if (user.Username != null)
                //{
                //    isFound = true;
                //}
                //USER _user = _data.USERS.Where(u => u.Username == userName).FirstOrDefault();

            }
        }
        catch (Exception)
        {

        }
        return isFound;
    }
    public bool HasRoles(string roles)
    {
        bool bFound = false;
        string[] _roles = roles.ToLower().Split(';');
        foreach (UserRole role in this.Roles)
        {
            try
            {
                bFound = _roles.Contains(role.RoleName.ToLower());
                if (bFound)
                    return bFound;
            }
            catch (Exception)
            {
            }
        }
        return bFound;
    }
    public Guid selectStatusType(String statusTypeName)
    {
        Guid statusId = Guid.Empty;
        using (ZB_FEPMS_Model db = new ZB_FEPMS_Model())
        {
            var statusTypeQuery = db.tbl_lu_Status.Where(r => r.name == statusTypeName).FirstOrDefault();
            if (statusTypeQuery != null)
            {
                statusId = statusTypeQuery.Id;
            }
        }
        return statusId;
    }

}

public class UserRole
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; }
    public List<RolePermission> Permissions = new List<RolePermission>();
}

public class RolePermission
{
    public Guid PermissionId { get; set; }
    public string PermissionDescription { get; set; }
}