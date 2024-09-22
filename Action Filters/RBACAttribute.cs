using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using ZB_FEPMS.Models;

public class RBACAttribute : AuthorizeAttribute
{
    public UserInfo checkUser(string username)
    {
        using (var db = new ZB_FEPMS_Model())
        {
            UserInfo checkUser = db.USERS.Where(u => u.Username.Equals(username)
            && (u.Inactive == null || u.Inactive == false)).ToList()
            .Select(ui => new UserInfo
            {
                userId = ui.UserId,
                username = ui.Username,
                fullName = getFullNameOfUser(ui.Firstname, ui.Lastname),
                roleName = ui.ROLES.Any() ? ui.ROLES.FirstOrDefault().RoleName : ""
            }).FirstOrDefault();
            return checkUser;
        }
    }

    public string getFullNameOfUser(string firstName, string lastName)
    {
        string fullName = "";
        if (!string.IsNullOrEmpty(firstName))
        {
            fullName = firstName;
        }
        if (!string.IsNullOrEmpty(lastName))
        {
            fullName += " " + lastName;
        }
        return fullName;
    }
    private string getUserHomeURL()
    {
        string URL = "";
        string role = HttpContext.Current.Session["userRole"].ToString();
        if (role.Equals("Officer"))
        {
            URL = "~/DashBoards/OfficerDashboard";
        }
        else if (role.Equals("Manager"))
        {
            URL = "~/DashBoards/ManagerDashboard";
        }
        else
        {
            URL = "~/Home/Unauthorised";
        }
        return URL;
    }

    private void saveLogin(ZB_FEPMS_Model dbe, string nonce)
    {
        Login login = new Login();
        login.nonce = nonce;
        login.date = DateTime.Now;
        dbe.Logins.Add(login);
        dbe.SaveChanges();
    }

    private Login previousLoginByNonce(ZB_FEPMS_Model dbe, string nonce)
    {
        Login previousLogin = dbe.Logins.FirstOrDefault(l => l.nonce.Equals(nonce));
        return previousLogin;
    }
    private void validatePermissionForExistingSession(AuthorizationContext filterContext)
    {
        using (var db = new ZB_FEPMS_Model())
        {
            string requestedPermission = String.Format("{0}-{1}",
                                filterContext.ActionDescriptor.ControllerDescriptor.ControllerName,
                                filterContext.ActionDescriptor.ActionName);
            if (requestedPermission.Equals("Home-Unauthorised"))
            {
                //Let the page display
            }
            else if (requestedPermission.Equals("Home-Index"))
            {
                filterContext.Result = new RedirectResult(getUserHomeURL());
            }
            else if (requestedPermission.Equals("Home-Logout"))
            {
                RBACUser rbacUserObj = new RBACUser();
                string operation = "LOGOUT";
                string object_id = HttpContext.Current.Session["userIdAttribute"].ToString();
                string username = HttpContext.Current.Session["userNameAttribute"].ToString();
                rbacUserObj.saveActivityLog(db, operation, object_id);
                filterContext.RequestContext.HttpContext.Session.Abandon();
                filterContext.RequestContext.HttpContext.Response.Cookies.Add(new HttpCookie("ASP.NET_SessionId", ""));
                return;
            }
            else
            {
                Guid userId = Guid.Parse(HttpContext.Current.Session["userIdAttribute"].ToString());
                bool userPermitted = db.USERS.Any(u => u.UserId.Equals(userId)
                && u.ROLES.Any(r => r.PERMISSIONS.Any(p => p.PermissionDescription.Equals(requestedPermission))));
                if (!userPermitted)
                {
                    filterContext.Result = new RedirectResult("~/Home/Unauthorised");
                }
            }
        }
    }

    private void validatePermissionForNewSession(AuthorizationContext filterContext)
    {
        using (var db = new ZB_FEPMS_Model())
        {
            string requestedPermission = String.Format("{0}-{1}",
                                filterContext.ActionDescriptor.ControllerDescriptor.ControllerName,
                                filterContext.ActionDescriptor.ActionName);
            if (requestedPermission.Equals("Home-Unauthorised"))
            {
                //Let the page display
            }
            else
            {
                Guid userId = Guid.Parse(HttpContext.Current.Session["userIdAttribute"].ToString());
                bool userPermitted = db.USERS.FirstOrDefault(u => u.UserId.Equals(userId))
                    .ROLES.Any(r => r.PERMISSIONS
                    .Any(p => p.PermissionDescription.Equals(requestedPermission)));
                if (!userPermitted)
                {
                    filterContext.Result = new RedirectResult("~/Home/Unauthorised");
                }
                else
                {
                    if (requestedPermission.Equals("Home-Index"))
                    {
                        filterContext.Result = new RedirectResult(getUserHomeURL());
                    }
                }
            }
        }
    }

    private void theLogoutPrompt(AuthorizationContext filterContext, string clientId)
    {
        string logoutURL = "https://auth.zemenbank.com/auth/realms/zemen/protocol/openid-connect/logout?"
              + "client_id=" + clientId;
        filterContext.Result = new RedirectResult(logoutURL);
        return;
    }
    private void newLogin(ZB_FEPMS_Model dbe, AuthorizationContext filterContext, string clientId, string redirectURI)
    {
        //New Login
        filterContext.RequestContext.HttpContext.Session.Abandon();
        filterContext.RequestContext.HttpContext.Response.Cookies.Add(new HttpCookie("ASP.NET_SessionId", ""));
        string SSONonce = Guid.NewGuid().ToString();
        string authReqUrl = "https://auth.zemenbank.com/auth/realms/zemen/protocol/" +
            "openid-connect/auth?client_id=" + clientId + "" +
            "&redirect_uri=" + redirectURI +
            "&response_type=code&scope=openid" +
            "&response_mode=form_post&nonce=" + SSONonce;
        saveLogin(dbe, SSONonce);
        filterContext.Result = new RedirectResult(authReqUrl);
    }

    public override void OnAuthorization(AuthorizationContext filterContext)
    {
        //string redirectURI = "http://127.0.0.1/ZB_FEPMS/Home/Index";
        //string redirectURI = "http://localhost:1926/Home/Index";
        //string redirectURI = "https://aps3.zemenbank.com/TestFEPMS/Home/Index";
        string redirectURI = "https://aps3.zemenbank.com/FEPMS/Home/Index";
        //#
        string Url = filterContext.RequestContext.HttpContext.Request.Url.AbsoluteUri;
        string RawUrl = filterContext.RequestContext.HttpContext.Request.RawUrl;
        string userRedirectURI = RawUrl.Equals("/") ? redirectURI : Url;
        //#
        //string clientId = "FEPMSTest";
        //string clientSecret = "L9k0e6qvhfmb494A2yAKCRNaTpyLhsSY";
        string clientId = "fepms";
        string clientSecret = "KZ8V13WoX6nbvbysOW1l5UASIA4V6oBD";
        if (HttpContext.Current.Session["userIdAttribute"] != null
            && !HttpContext.Current.Session.IsNewSession)
        {
            //Check permission of requesting user
            validatePermissionForExistingSession(filterContext);
        }
        else
        {
            using (var dbe = new ZB_FEPMS_Model())
            {
                var reponseForm = filterContext.RequestContext.HttpContext.Request.Form;
                if (reponseForm != null && !string.IsNullOrEmpty(reponseForm["code"]))
                {
                    string code = reponseForm["code"];
                    string state = reponseForm["state"];
                    string tokenReqUrl = "https://auth.zemenbank.com/auth/realms/zemen/protocol/openid-connect/token";
                    string tokenReqParams = "code=" + code + "&client_id=" + clientId +
                        "&client_secret=" + clientSecret + "&redirect_uri=" + userRedirectURI +
                        "&grant_type=authorization_code";
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                        string response = "";
                        try
                        {
                            response = wc.UploadString(tokenReqUrl, tokenReqParams);
                        }
                        catch (Exception exc)
                        {
                            ContentResult contentResult = new ContentResult
                            {
                                Content = "<h3 style=\"color:red\">You are not authorized to access this resource!</h3><br/>" +
                                        "Contact the system administrator or your supervisor. wc.",
                                ContentType = "text/html",
                            };
                            filterContext.Result = contentResult;
                            return;
                        }
                        var tokenReqValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
                        string id_token = tokenReqValues["id_token"];
                        var issuer = "https://auth.zemenbank.com/auth/realms/zemen";
                        var validatedToken = ValidateToken(id_token, issuer, clientId);
                        bool tokenIsValid = false;
                        Login currentUserPrevLogin = null;
                        if (validatedToken == null)
                        {
                            //Invalid token
                            tokenIsValid = false;
                        }
                        else
                        {
                            tokenIsValid = true;
                            // Additional validation...
                            // Validate alg
                            var expectedAlg = SecurityAlgorithms.RsaSha256;
                            if (validatedToken.Header?.Alg == null || validatedToken.Header?.Alg != expectedAlg)
                            {
                                //Invalid token
                                tokenIsValid = false;
                            }
                            // Validate nonce
                            validatedToken.Payload.TryGetValue("nonce", out var rawNonce);
                            string nonce = rawNonce.ToString();
                            if (string.IsNullOrEmpty(nonce))
                            {
                                //Invalid token
                                tokenIsValid = false;
                            }
                            else
                            {
                                currentUserPrevLogin = previousLoginByNonce(dbe, nonce);
                                if (currentUserPrevLogin == null)
                                {
                                    //Invalid token
                                    tokenIsValid = false;
                                }
                                else
                                {
                                    var expectedNonce = currentUserPrevLogin.nonce;
                                    bool nonceMatches = nonce.Equals(expectedNonce);
                                    if (!nonceMatches)
                                    {
                                        //Invalid token
                                        tokenIsValid = false;
                                        dbe.Logins.RemoveRange(dbe.Logins
                                            .Where(l => l.user_name.Equals(currentUserPrevLogin.user_name)
                                            || DateTime.Now.AddDays(-1) > l.date));
                                        dbe.SaveChanges();
                                    }
                                }
                            }
                        }
                        if (tokenIsValid)
                        {
                            validatedToken.Payload.TryGetValue("preferred_username", out var rawUsername);
                            string username = rawUsername.ToString();
                            UserInfo loggedInUser = checkUser(username);
                            if (loggedInUser != null)
                            {
                                HttpContext.Current.Session["clientId"] = clientId;
                                HttpContext.Current.Session["userIdAttribute"] = loggedInUser.userId;
                                HttpContext.Current.Session["userNameAttribute"] = loggedInUser.username;
                                HttpContext.Current.Session["userRole"] = loggedInUser.roleName;
                                HttpContext.Current.Session["fullNameAttribute"] = loggedInUser.fullName;
                                HttpContext.Current.Session["navType"] = "nav-md";
                                currentUserPrevLogin.user_name = loggedInUser.username;
                                DateTime yesterday = DateTime.Now.AddDays(-1);
                                dbe.Logins.RemoveRange(dbe.Logins
                                        .Where(l => (l.user_name.Equals(currentUserPrevLogin.user_name)
                                        && !l.id.Equals(currentUserPrevLogin.id))
                                        || yesterday > l.date));
                                RBACUser rbacUserObj = new RBACUser();
                                string operation = "LOGIN";
                                string object_id = HttpContext.Current.Session["userIdAttribute"].ToString();
                                rbacUserObj.saveActivityLog(dbe, operation, object_id);
                                validatePermissionForNewSession(filterContext);
                            }
                            else
                            {
                                //User is not registered on FEPMS
                                ContentResult contentResult = new ContentResult
                                {
                                    Content = "<h3 style=\"color:red\">You are not authorized to access this resource!</h3><br/>" +
                                        "Contact the system administrator or your supervisor. wcnot",
                                    ContentType = "text/html",
                                };
                                filterContext.Result = contentResult;
                            }
                        }
                        else
                        {
                            //Prompt for a logout if login is invalid
                            theLogoutPrompt(filterContext, clientId);
                        }
                    }
                }
                else
                {
                    //New Login
                    newLogin(dbe, filterContext, clientId, userRedirectURI);
                }
            }
        }
    }

    private JwtSecurityToken ValidateToken(string token, string issuer, string clientId)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new ArgumentNullException(nameof(token));
        }
        if (string.IsNullOrEmpty(issuer))
        {
            throw new ArgumentNullException(nameof(issuer));
        }
        string discoveryDocumentURL = "https://auth.zemenbank.com/auth/realms/zemen/.well-known/openid-configuration";
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(discoveryDocumentURL, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever());
        var discoveryDocument = configManager.GetConfigurationAsync(default).Result;
        var signingKeys = discoveryDocument.SigningKeys;
        var validationParameters = new TokenValidationParameters
        {
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            ValidateLifetime = true,
            // Allow for some drift in server time
            // (a lower value is better; we recommend two minutes or less)
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidateAudience = true,
            ValidAudience = clientId, // This Application's Client ID
        };
        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, validationParameters, out var rawValidatedToken);
            return (JwtSecurityToken)rawValidatedToken;
        }
        catch (SecurityTokenValidationException exc)
        {
            return null;
        }
    }
}
