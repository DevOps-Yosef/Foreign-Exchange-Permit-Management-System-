using System;
using System.Web;
using System.Web.Mvc;

//Get requesting user's roles/permissions from database tables...      
public static class RBAC_ExtendedMethods
{
    public static bool HasRole(this ControllerBase controller, string role, String userName)
    {
        bool bFound = false;
        try
        {

            //Check if the requesting user has the specified role...
            bFound = new RBACUser(userName).HasRole(role);
        }
        catch { }
        return bFound;
    }

    public static bool HasRole(this ControllerBase controller, String role)
    {
        bool bFound = false;
        try
        {
            String userName = null;
            userName = System.Web.HttpContext.Current.Session["userNameAttribute"] as String;
            //Check if the requesting user has the specified role...
            bFound = new RBACUser(userName).HasRole(role);
        }
        catch { }
        return bFound;
    }

    public static bool IsValidUser(this ControllerBase controller, String userName, String password)
    {
        bool bFound = false;

        bFound = new RBACUser(userName).isUserValid(userName, password);
        return bFound;
    }

    public static string getFullNameOfUserByUsername(this ControllerBase controller, string _userName)
    {
        return new RBACUser(controller.ControllerContext.HttpContext.User.Identity.Name).getFullNameOfUserByUsername(_userName);
    }
    public static string getFullNameOfUserById(this ControllerBase controller, string _userId)
    {
        return new RBACUser(controller.ControllerContext.HttpContext.User.Identity.Name).getFullNameOfUserById(_userId);
    }
    public static string getRoleOfUserById(this ControllerBase controller, string _userId)
    {
        return new RBACUser(controller.ControllerContext.HttpContext.User.Identity.Name).getRoleOfUserById(_userId);
    }

    public static string getRoleOfCurrentUser(this ControllerBase controller)
    {
        return new RBACUser(controller.ControllerContext.HttpContext.User.Identity.Name).getRoleOfCurrentUser();
    }
    public static bool HasLoggedIn(this ControllerBase controller)
    {
        bool bFound = false;
        string userNameAttribute = HttpContext.Current.Session["userNameAttribute"] != null
            ? HttpContext.Current.Session["userNameAttribute"].ToString() : "";
        if (!string.IsNullOrEmpty(userNameAttribute))
        {
            bFound = true;
        }
        return bFound;
    }
    public static String getUserName(this ControllerBase controller)
    {
        string userNameAttribute = HttpContext.Current.Session["fullNameAttribute"] != null
            ? HttpContext.Current.Session["fullNameAttribute"].ToString() : "";
        return userNameAttribute;
    }
    //public static string userIdToken(this ControllerBase controller)
    //{
    //    return HttpContext.Current.Session["userIdToken"] != null
    //        ? HttpContext.Current.Session["userIdToken"].ToString() : "";
    //}
    public static string clientId(this ControllerBase controller)
    {
        return HttpContext.Current.Session["clientId"] != null
            ? HttpContext.Current.Session["clientId"].ToString() : "";
    }
    public static String getNavType(this ControllerBase controller)
    {
        string navType = HttpContext.Current.Session["navType"] != null
            ? HttpContext.Current.Session["navType"].ToString() : "";
        return navType;
    }

    public static String getNumberOfMonths(this ControllerBase controller, DateTime dateFrom, DateTime dateTo)
    {
        return (((dateTo.Year - dateFrom.Year) * 12) + dateTo.Month - dateFrom.Month).ToString("N0");
    }


    public static bool HasRoles(this ControllerBase controller, string roles)
    {
        bool bFound = false;
        try
        {
            //Check if the requesting user has any of the specified roles...
            //Make sure you separate the roles using ; (ie "Sales Manager;Sales Operator"
            bFound = new RBACUser(controller.ControllerContext.HttpContext.User.Identity.Name).HasRoles(roles);
        }
        catch { }
        return bFound;
    }
    public static bool IsSysAdmin(this ControllerBase controller, String userName)
    {
        bool bIsSysAdmin = false;
        try
        {
            //Check if the requesting user has the System Administrator privilege...
            bIsSysAdmin = new RBACUser(userName).IsSysAdmin;
        }
        catch { }
        return bIsSysAdmin;
    }

}
