using Oracle.ManagedDataAccess.Client;
using PagedList;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Script.Serialization;
using ZB_FEPMS.Action_Filters;
using ZB_FEPMS.Models;

namespace ZB_FEPMS.Controllers
{

    [RBAC]
    [NoCache]
    public class MerchantController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
        private int sizeOfPage = 15;
        int numberOfPage = 1;

        public CurrencyDetails returnCurrencyDetails(string currencyType, decimal amount)
        {
            CurrencyDetails currencyDetails = new CurrencyDetails();
            decimal currencyRate = 0;
            decimal USDRate = 0;
            decimal AmountInBirr = 0;
            decimal AmountInUSD = 0;
            string connectionString = ConfigurationManager.ConnectionStrings["ConnectionString3"].ConnectionString;
            OracleConnection connection = new OracleConnection(connectionString);
            connection.Open();
            decimal rate = 0;
            using (var dbe = new ZB_FEPMS_Model())
            {
                using (var dbeTransaction = dbe.Database.BeginTransaction())
                {
                    SETTING setting = dbe.SETTINGs.FirstOrDefault();
                    rate = 1 + (setting.RATE_PERCENTAGE.Value / 100);
                }
            }
            string sqlQuery = "SELECT round(BUY_RATE*" + rate.ToString() + ",4) FROM FCUBSPRD.CYTM_RATES " +
                "WHERE BRANCH_CODE='102' AND RATE_TYPE='TC_CASH' and " +
                "CCY1 = :CurrencyType ORDER BY RATE_DATE DESC";
            OracleCommand command = new OracleCommand(sqlQuery, connection);
            command.Parameters.Add(new OracleParameter("CurrencyType", currencyType));
            OracleDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                currencyRate = reader.GetDecimal(0);
            }
            command = new OracleCommand(sqlQuery, connection);
            command.Parameters.Add(new OracleParameter("CurrencyType", "USD"));
            reader = command.ExecuteReader();
            if (reader.Read())
            {
                USDRate = reader.GetDecimal(0);
            }
            AmountInBirr = amount * currencyRate;
            AmountInUSD = USDRate > 0 ? AmountInBirr / USDRate : 0;
            currencyDetails.CurrencyRate = currencyRate;
            currencyDetails.USDRate = USDRate;
            currencyDetails.AmountInBirr = AmountInBirr;
            currencyDetails.AmountInUSD = AmountInUSD;
            reader.Close();
            reader.Dispose();
            command.Dispose();
            connection.Close();
            connection.Dispose();
            return currencyDetails;
        }

        public List<string> selectListOfCurrencyTypes()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConnectionString3"].ConnectionString;
            OracleConnection connection = new OracleConnection(connectionString);
            connection.Open();
            string sqlQuery = "SELECT DISTINCT CCY1 FROM FCUBSPRD.CYTM_RATES_MASTER WHERE BRANCH_CODE='102' " +
                "AND TO_CHAR(CHECKER_DT_STAMP, 'DD-MON-YYYY')=(SELECT TO_CHAR ( SYSDATE, 'DD-MON-YYYY') from dual ) " +
                "ORDER BY CCY1";
            OracleCommand command = new OracleCommand(sqlQuery, connection);
            OracleDataReader reader = command.ExecuteReader();
            List<string> currencyTypeList = new List<string>();
            while (reader.Read())
            {
                currencyTypeList.Add(reader.GetString(0));
            }
            reader.Close();
            reader.Dispose();
            command.Dispose();
            connection.Close();
            connection.Dispose();
            return currencyTypeList;
        }

        public string selectCurrentYear()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConnectionString3"].ConnectionString;
            OracleConnection connection = new OracleConnection(connectionString);
            connection.Open();
            string sqlQuery = "SELECT EXTRACT(YEAR FROM SYSDATE) FROM DUAL";
            OracleCommand command = new OracleCommand(sqlQuery, connection);
            OracleDataReader reader = command.ExecuteReader();
            string currentYear = "";
            if (reader.Read())
            {
                currentYear = reader.GetString(0);
            }
            reader.Close();
            reader.Dispose();
            command.Dispose();
            connection.Close();
            connection.Dispose();
            return currentYear;
        }

        public int returnNextSerialNumberValueByType(ZB_FEPMS_Model dbe, string serialNumberType, string currentYear)
        {
            tblSerialNumberShelf serialNumberShelf = dbe.tblSerialNumberShelves
                .FirstOrDefault(tsns => tsns.SerialNumberType.Equals(serialNumberType)
                && tsns.Year.Equals(currentYear)
                && tsns.IsLatest == true);
            if (serialNumberShelf != null)
            {
                int serialNumberValue = serialNumberShelf.SerialNumberValue;
                return ++serialNumberValue;
            }
            else
            {
                serialNumberShelf = new tblSerialNumberShelf();
                serialNumberShelf.SerialNumberType = serialNumberType;
                serialNumberShelf.SerialNumberValue = 0;
                serialNumberShelf.IsLatest = true;
                serialNumberShelf.Year = currentYear;
                dbe.tblSerialNumberShelves.Add(serialNumberShelf);
                dbe.SaveChanges();
                int serialNumberValue = serialNumberShelf.SerialNumberValue;
                return ++serialNumberValue;
            }
        }

        public decimal formatDecimal(string decimalValue)
        {
            decimalValue = decimalValue.Replace(",", "");
            return decimal.Parse(decimalValue);
        }

        //$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$
        public tblPermitAmount initUpdatePurchaseOrderPermitAmountRequestForm(Guid Id)
        {
            //xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
            tblPermit permit = db.tblPermits.Find(Id);
            tblPermitAmount permitAmount = new tblPermitAmount();
            permitAmount.tblPermit = permit;
            permitAmount.PermitId = permitAmount.tblPermit.Id;
            permitAmount.MerchantId = permitAmount.tblPermit.MerchantId;
            return permitAmount;
        }

        public ActionResult UpdatePurchaseOrderPermitAmountRequest(Guid Id)
        {
            return View(initUpdatePurchaseOrderPermitAmountRequestForm(Id));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdatePurchaseOrderPermitAmountRequest(tblPermitAmount permitAmount)
        {
            bool sendEmail = false;
            if (string.IsNullOrEmpty(permitAmount.Reason))
            {
                ModelState.AddModelError("Reason", "Required.");
            }
            if (string.IsNullOrEmpty(permitAmount.CurrencyRateValue)
                || permitAmount.CurrencyRateValue.Equals("0"))
            {
                ModelState.AddModelError("CurrencyRateValue", "Required.");
            }
            if (string.IsNullOrEmpty(permitAmount.AmountValue)
                || permitAmount.AmountValue.Equals("0"))
            {
                ModelState.AddModelError("AmountValue", "Required.");
            }
            if (string.IsNullOrEmpty(permitAmount.USDRateValue)
                || permitAmount.USDRateValue.Equals("0"))
            {
                ModelState.AddModelError("USDRateValue", "Required.");
            }
            if (string.IsNullOrEmpty(permitAmount.AmountInUSDValue)
                || permitAmount.AmountInUSDValue.Equals("0"))
            {
                ModelState.AddModelError("AmountInUSDValue", "Required.");
            }
            if (string.IsNullOrEmpty(permitAmount.AmountInBirrValue)
                || permitAmount.AmountInBirrValue.Equals("0"))
            {
                ModelState.AddModelError("AmountInBirrValue", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            tblPermitAmount _PermitAmount = new tblPermitAmount();
                            _PermitAmount.PermitId = permitAmount.PermitId;
                            _PermitAmount.MerchantId = permitAmount.MerchantId;
                            _PermitAmount.ApprovalStatusId = dbe.tbl_lu_Status.FirstOrDefault(tls => tls.name.Equals("Pending")).Id;
                            _PermitAmount.CreatedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                            _PermitAmount.CreatedDate = DateTime.Now;
                            _PermitAmount.Reason = permitAmount.Reason;
                            dbe.tblPermitAmounts.Add(_PermitAmount);
                            dbe.SaveChanges();
                            tblPermitAmountDetail permitAmountDetail = new tblPermitAmountDetail();
                            permitAmountDetail.PermitAmountId = _PermitAmount.Id;
                            permitAmountDetail.CurrencyRate = formatDecimal(permitAmount.CurrencyRateValue);
                            permitAmountDetail.Amount = formatDecimal(permitAmount.AmountValue);
                            permitAmountDetail.USDRate = formatDecimal(permitAmount.USDRateValue);
                            permitAmountDetail.AmountInUSD = formatDecimal(permitAmount.AmountInUSDValue);
                            permitAmountDetail.AmountInBirr = formatDecimal(permitAmount.AmountInBirrValue);
                            dbe.tblPermitAmountDetails.Add(permitAmountDetail);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-UpdatePurchaseOrderPermitAmountRequest";
                            string object_id = _PermitAmount.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Successfully submitted for approval!";
                            sendEmail = true;
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                        if (sendEmail)
                        {
                            int purchaseOrderPermitAmountUpdatePendingCount = db.tblPermitAmounts
                                .Where(tpa => tpa.tbl_lu_Status.name.Equals("Pending")
                                && tpa.tblPermit.tblSerialNumberShelf.SerialNumberType.Equals("PO")).Count();
                            string messageBody = "You have <span style=\"font-weight: bold; text-decoration: underline \">"
                                + purchaseOrderPermitAmountUpdatePendingCount + " purchase order permit amount update</span> " +
                                "waiting approval.";
                            List<string> mailAddressList = dbe.USERS.Where(u => u
                            .ROLES.Any(r => r.RoleName.Equals("Manager")))
                                .Select(u => u.EMail + "#" + u.Firstname).ToList();
                            //mailAddressList.Clear();
                            new RBACUser().sendEmail(mailAddressList, messageBody, "Merchant/UpdatePurchaseOrderPermitAmount_Auth");
                        }
                        return RedirectToAction("PurchaseOrderPermits", new RouteValueDictionary(new { merchantId = permitAmount.MerchantId }));
                    }
                }
            }
            return View(initUpdatePurchaseOrderPermitAmountRequestForm(permitAmount.Id));
        }
        public tblPermitAmount initUpdateImportPermitAmountRequestForm(Guid Id)
        {
            tblPermit permit = db.tblPermits.Find(Id);
            tblPermitAmount permitAmount = new tblPermitAmount();
            permitAmount.tblPermit = permit;
            permitAmount.PermitId = permitAmount.tblPermit.Id;
            permitAmount.MerchantId = permitAmount.tblPermit.MerchantId;
            permitAmount.MethodOfPaymentId = permitAmount.tblPermit.MethodOfPaymentId;
            permitAmount.MethodOfPaymentName = permitAmount.tblPermit.tbl_lu_MethodOfPayment.name;
            permitAmount.CurrencyType = permitAmount.tblPermit.CurrencyType;
            CurrencyDetails currencyDetails = new CurrencyDetails();
            try
            {
                currencyDetails = returnCurrencyDetails(permitAmount.CurrencyType, 0);
            }
            catch (Exception ex) { }
            permitAmount.CurrencyRateValue = currencyDetails.CurrencyRate.ToString("N6");
            permitAmount.USDRateValue = currencyDetails.USDRate.ToString("N6");
            permitAmount.ImportPOList = permit.tblMerchant.tblPermits
                .Where(tp => tp.tblSerialNumberShelf.SerialNumberType.Equals("PO")
                && tp.RemainingAmount > 0 && tp.CurrencyType.Equals(permitAmount.CurrencyType))
                .Select(c => new tblPermit()
                {
                    Id = c.Id,
                    PermitNumber = c.PermitNumber,
                    Status = c.tbl_lu_Status.name,
                    CurrencyType = c.CurrencyType,
                    RemainingAmount = c.RemainingAmount,
                    ExpiredYesNo = c.tblPOPermitExpiries.Any(tppe => tppe.ExpiryDate >= DateTime.Now.Date)
                    ? "No" : "Yes",
                    Date = c.Date
                }).OrderByDescending(tp => tp.Date).ToList();
            return permitAmount;
        }

        public tblPermitAmount initUpdateImportPermitAmountRequestFormError(tblPermitAmount permitAmount)
        {
            tblPermit permit = db.tblPermits.Find(permitAmount.PermitId);
            permitAmount.tblPermit = permit;
            permitAmount.ImportPOList = permit.tblMerchant.tblPermits
                .Where(tp => tp.tblSerialNumberShelf.SerialNumberType.Equals("PO")
                && tp.RemainingAmount > 0 && tp.CurrencyType.Equals(permitAmount.CurrencyType))
                .Select(c => new tblPermit()
                {
                    Id = c.Id,
                    PermitNumber = c.PermitNumber,
                    Status = c.tbl_lu_Status.name,
                    CurrencyType = c.CurrencyType,
                    RemainingAmount = c.RemainingAmount,
                    ExpiredYesNo = c.tblPOPermitExpiries.Any(tppe => tppe.ExpiryDate >= DateTime.Now.Date)
                    ? "No" : "Yes",
                    Date = c.Date
                }).OrderByDescending(tp => tp.Date).ToList();
            return permitAmount;
        }

        public ActionResult UpdateImportPermitAmountRequest(Guid Id)
        {
            return View(initUpdateImportPermitAmountRequestForm(Id));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateImportPermitAmountRequest(tblPermitAmount permitAmount)
        {
            bool sendEmail = false;
            bool POMethodOfUpdateIsSelected = false;
            bool AmountMethodOfUpdateIsSelected = false;
            using (var dbe = new ZB_FEPMS_Model())
            {
                using (var dbeTransaction = dbe.Database.BeginTransaction())
                {
                    if (string.IsNullOrEmpty(permitAmount.Reason))
                    {
                        ModelState.AddModelError("Reason", "Required.");
                    }
                    foreach (tblPermit permitObj in permitAmount.ImportPOList)
                    {
                        if ((!string.IsNullOrEmpty(permitObj.AmountUpdateValue)
                            && !permitObj.AmountUpdateValue.Equals("0"))
                            || (!string.IsNullOrEmpty(permitObj.AmountInUSDValue)
                            && !permitObj.AmountInUSDValue.Equals("0"))
                            || (!string.IsNullOrEmpty(permitObj.AmountInBirrValue)
                            && !permitObj.AmountInBirrValue.Equals("0")))
                        {
                            POMethodOfUpdateIsSelected = true;
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(permitAmount.AmountValue)
                        && !permitAmount.AmountValue.Equals("0")
                        && !string.IsNullOrEmpty(permitAmount.AmountInUSDValue)
                        && !permitAmount.AmountInUSDValue.Equals("0")
                        && !string.IsNullOrEmpty(permitAmount.AmountInBirrValue)
                        && !permitAmount.AmountInBirrValue.Equals("0")
                        && !string.IsNullOrEmpty(permitAmount.CurrencyRateValue)
                        && !permitAmount.CurrencyRateValue.Equals("0")
                        && !string.IsNullOrEmpty(permitAmount.USDRateValue)
                        && !permitAmount.USDRateValue.Equals("0"))
                    {
                        AmountMethodOfUpdateIsSelected = true;
                    }
                    if (AmountMethodOfUpdateIsSelected && POMethodOfUpdateIsSelected)
                    {
                        ModelState.AddModelError("MethodOfPaymentId", "Please enter PO amount below or Amount above. Not both");
                        return View(initUpdateImportPermitAmountRequestFormError(permitAmount));
                    }
                    else if (!AmountMethodOfUpdateIsSelected && !POMethodOfUpdateIsSelected)
                    {
                        ModelState.AddModelError("MethodOfPaymentId", "Please enter PO amount below or Amount above.");
                        return View(initUpdateImportPermitAmountRequestFormError(permitAmount));
                    }
                    else
                    {
                        if (ModelState.IsValid)
                        {
                            try
                            {
                                List<Guid> permitIds = new List<Guid>();
                                List<tblPermit> permitList = null;
                                bool errorHappened = false;
                                tblPermitAmount _PermitAmount = new tblPermitAmount();
                                _PermitAmount.PermitId = permitAmount.PermitId;
                                _PermitAmount.MerchantId = permitAmount.MerchantId;
                                _PermitAmount.ApprovalStatusId = dbe.tbl_lu_Status.FirstOrDefault(tls => tls.name.Equals("Pending")).Id;
                                _PermitAmount.CreatedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                                _PermitAmount.CreatedDate = DateTime.Now;
                                _PermitAmount.Reason = permitAmount.Reason;
                                dbe.tblPermitAmounts.Add(_PermitAmount);
                                dbe.SaveChanges();
                                if (POMethodOfUpdateIsSelected)
                                {
                                    foreach (tblPermit permitObj in permitAmount.ImportPOList)
                                    {
                                        permitIds.Add(permitObj.Id);
                                    }
                                    permitList = dbe.tblPermits.Where(tp => permitIds.Contains(tp.Id)).ToList();
                                    int counter = 0;
                                    foreach (tblPermit permitObj in permitAmount.ImportPOList)
                                    {
                                        if (!string.IsNullOrEmpty(permitObj.AmountUpdateValue))
                                        {
                                            tblPermit _permitObj = permitList.FirstOrDefault(pl => pl.Id.Equals(permitObj.Id));
                                            if ((_permitObj.RemainingAmount - formatDecimal(permitObj.AmountUpdateValue)) < 0)
                                            {
                                                ModelState.AddModelError("ImportPOList[" + counter + "].AmountUpdateValue", "Amount is greater than remaining amount.");
                                                errorHappened = true;
                                            }
                                            if (!_permitObj.tblPOPermitExpiries.Any(tppe => tppe.ExpiryDate >= DateTime.Now.Date))
                                            {
                                                ModelState.AddModelError("ImportPOList[" + counter + "].AmountUpdateValue", "PO is expired.");
                                                errorHappened = true;
                                            }
                                            if (!permitObj.Status.Equals("Active"))
                                            {
                                                ModelState.AddModelError("ImportPOList[" + counter + "].AmountUpdateValue", "PO is " + permitObj.Status);
                                                errorHappened = true;
                                            }
                                        }
                                        counter++;
                                    }
                                }
                                if (errorHappened)
                                {
                                    return View(initUpdateImportPermitAmountRequestFormError(permitAmount));
                                }
                                //$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$
                                if (POMethodOfUpdateIsSelected)
                                {
                                    decimal Amount = 0, AmountInUSD = 0, AmountInBirr = 0;
                                    foreach (tblPermit permitObj in permitAmount.ImportPOList)
                                    {
                                        if (!string.IsNullOrEmpty(permitObj.AmountUpdateValue))
                                        {
                                            if (!string.IsNullOrEmpty(permitObj.AmountUpdateValue)
                                            && !permitObj.AmountUpdateValue.Equals("0")
                                            && !string.IsNullOrEmpty(permitObj.AmountInUSDValue)
                                            && !permitObj.AmountInUSDValue.Equals("0")
                                            && !string.IsNullOrEmpty(permitObj.AmountInBirrValue)
                                            && !permitObj.AmountInBirrValue.Equals("0"))
                                            {
                                                tblPermit _permitObj = permitList.FirstOrDefault(pl => pl.Id.Equals(permitObj.Id));
                                                if (_permitObj.tblSerialNumberShelf.SerialNumberType.Equals("PO")
                                                     && _permitObj.RemainingAmount > 0
                                                     && ((_permitObj.RemainingAmount - formatDecimal(permitObj.AmountUpdateValue)) >= 0)
                                                     && _permitObj.CurrencyType.Equals(permitAmount.CurrencyType)
                                                     && _permitObj.tbl_lu_Status.name.Equals("Active")
                                                     && _permitObj.tblPOPermitExpiries
                                                     .Any(tppe => tppe.ExpiryDate >= DateTime.Now.Date))
                                                {
                                                    tblPOPermitAmountDetail pOPermitAmountDetail = new tblPOPermitAmountDetail();
                                                    pOPermitAmountDetail.PermitId = _permitObj.Id;
                                                    pOPermitAmountDetail.PermitAmountId = _PermitAmount.Id;
                                                    pOPermitAmountDetail.CurrencyRate = formatDecimal(permitAmount.CurrencyRateValue);
                                                    pOPermitAmountDetail.USDRate = formatDecimal(permitAmount.USDRateValue);
                                                    pOPermitAmountDetail.Amount = formatDecimal(permitObj.AmountUpdateValue);
                                                    pOPermitAmountDetail.AmountInUSD = formatDecimal(permitObj.AmountInUSDValue);
                                                    pOPermitAmountDetail.AmountInBirr = formatDecimal(permitObj.AmountInBirrValue);
                                                    dbe.tblPOPermitAmountDetails.Add(pOPermitAmountDetail);
                                                    dbe.SaveChanges();
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (AmountMethodOfUpdateIsSelected)
                                {
                                    tblPermitAmountDetail permitAmountDetail = new tblPermitAmountDetail();
                                    permitAmountDetail.PermitAmountId = _PermitAmount.Id;
                                    permitAmountDetail.CurrencyRate = formatDecimal(permitAmount.CurrencyRateValue);
                                    permitAmountDetail.Amount = formatDecimal(permitAmount.AmountValue);
                                    permitAmountDetail.USDRate = formatDecimal(permitAmount.USDRateValue);
                                    permitAmountDetail.AmountInUSD = formatDecimal(permitAmount.AmountInUSDValue);
                                    permitAmountDetail.AmountInBirr = formatDecimal(permitAmount.AmountInBirrValue);
                                    dbe.tblPermitAmountDetails.Add(permitAmountDetail);
                                    dbe.SaveChanges();
                                }
                                //$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$
                                RBACUser rbacUserObj = new RBACUser();
                                string operation = "Merchant-UpdateImportPermitAmountRequest";
                                string object_id = _PermitAmount.Id.ToString();
                                rbacUserObj.saveActivityLog(dbe, operation, object_id);
                                dbeTransaction.Commit();
                                TempData["successMsg"] = "Successfully submitted for approval!";
                                sendEmail = true;
                            }
                            catch (Exception exc)
                            {
                                dbeTransaction.Rollback();
                                TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                            }
                            if (sendEmail)
                            {
                                int importPermitAmountUpdatePendingCount = db.tblPermitAmounts
                                    .Where(tpa => tpa.tbl_lu_Status.name.Equals("Pending")
                                    && tpa.tblPermit.tblSerialNumberShelf.SerialNumberType.Equals("IMP")).Count();
                                string messageBody = "You have <span style=\"font-weight: bold; text-decoration: underline \">"
                                    + importPermitAmountUpdatePendingCount + " import permit amount update</span> " +
                                    "waiting approval.";
                                List<string> mailAddressList = dbe.USERS.Where(u => u
                                .ROLES.Any(r => r.RoleName.Equals("Manager")))
                                    .Select(u => u.EMail + "#" + u.Firstname).ToList();
                                //mailAddressList.Clear();
                                new RBACUser().sendEmail(mailAddressList, messageBody, "Merchant/UpdateImportPermitAmount_Auth");
                            }
                            return RedirectToAction("ImportPermits", new RouteValueDictionary(new { merchantId = permitAmount.MerchantId }));
                        }
                    }
                }
            }
            return View(initUpdateImportPermitAmountRequestFormError(permitAmount));
        }

        public JsonResult fillTheAmounts(string currencyType, string amount)
        {
            string result = "";
            try
            {
                amount = string.IsNullOrEmpty(amount) ? "0" : amount;
                if (!string.IsNullOrEmpty(currencyType))
                {
                    db.Configuration.ProxyCreationEnabled = false;
                    decimal _Amount = decimal.Parse(amount);
                    CurrencyDetails currencyDetails = new CurrencyDetails();
                    currencyDetails = returnCurrencyDetails(currencyType, _Amount);
                    JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
                    result = javaScriptSerializer.Serialize(currencyDetails);
                }
            }
            catch (Exception exc)
            {
                int aaa = 333;
            }
            return Json(result ?? "", JsonRequestBehavior.AllowGet);
        }
        public ActionResult Index(string tinNumber, string NBENumber, string importerName
            , string permitNumber, string PONumber, int? page)
        {
            numberOfPage = (page ?? 1);
            var merchantList = db.tblMerchants.Where(tm => tm.Id != null);
            if (!string.IsNullOrEmpty(tinNumber))
            {
                merchantList = merchantList.Where(tm => tm.TinNumber.Contains(tinNumber.Trim()));
            }
            if (!string.IsNullOrEmpty(NBENumber))
            {
                merchantList = merchantList.Where(tm => tm.NBENumber.Contains(NBENumber.Trim()));
            }
            if (!string.IsNullOrEmpty(importerName))
            {
                merchantList = merchantList.Where(tm => tm.ImporterName.Contains(importerName.Trim()));
            }
            if (!string.IsNullOrEmpty(permitNumber))
            {
                merchantList = merchantList.Where(tm => tm.tblPermits.Any(tp => tp.PermitNumber.Contains(permitNumber.Trim())));
                ViewBag.permitNumber = permitNumber.Trim();
            }
            if (!string.IsNullOrEmpty(PONumber))
            {
                merchantList = merchantList.Where(tm => tm.tblPermits.Any(tp => tp.PermitNumber.Contains(PONumber.Trim())));
                ViewBag.PONumber = PONumber.Trim();
            }
            merchantList = merchantList.OrderBy(tm => tm.ImporterName);
            return View(merchantList.ToPagedList(numberOfPage, sizeOfPage));
        }
        public ActionResult PurchaseOrderPermits(int? page, Guid? merchantId, string PONumber)
        {
            numberOfPage = (page ?? 1);
            if (string.IsNullOrEmpty(PONumber))
            {
                if (merchantId.HasValue)
                {
                    var permitList = db.tblPermits.Where(tp => tp.MerchantId.Equals(merchantId.Value)
                    && tp.tblSerialNumberShelf.SerialNumberType.Equals("PO")).OrderByDescending(tp => tp.Date);
                    tblMerchant merchant = db.tblMerchants.Find(merchantId.Value);
                    ViewBag.ImporterName = merchant.ImporterName;
                    ViewBag.TinNumber = merchant.TinNumber;
                    ViewBag.NBENumber = merchant.NBENumber;
                    ViewBag.merchantId = merchant.Id;
                    return View(permitList.ToPagedList(numberOfPage, sizeOfPage));
                }
                else
                {
                    TempData["sErrMsg"] = "Enter PO#.";
                    return RedirectToAction("Index", new RouteValueDictionary(new { PONumber = PONumber }));
                }

            }
            else
            {
                PONumber = PONumber.Trim();
                var permitCount = db.tblPermits.Where(tp => tp.PermitNumber.Contains(PONumber)).Count();
                if (permitCount == 0)
                {
                    TempData["sErrMsg"] = "PO not found.";
                    return RedirectToAction("Index", new RouteValueDictionary(new { PONumber = PONumber }));
                }
                else if (permitCount != 1)
                {
                    TempData["sErrMsg"] = "Enter full PO#.";
                    return RedirectToAction("Index", new RouteValueDictionary(new { PONumber = PONumber }));
                }
                else
                {
                    var permitList = db.tblPermits.Where(tp => tp.PermitNumber.Contains(PONumber))
                        .Take(1).OrderByDescending(tp => tp.Date);
                    tblMerchant merchant = permitList.ToList().FirstOrDefault().tblMerchant;
                    ViewBag.ImporterName = merchant.ImporterName;
                    ViewBag.TinNumber = merchant.TinNumber;
                    ViewBag.NBENumber = merchant.NBENumber;
                    ViewBag.merchantId = merchant.Id;
                    ViewBag.PONumber = PONumber;
                    return View(permitList.ToPagedList(numberOfPage, sizeOfPage));
                }
            }
        }

        public ActionResult ImportPermits(int? page, Guid? merchantId, string permitNumber)
        {
            numberOfPage = (page ?? 1);
            if (string.IsNullOrEmpty(permitNumber))
            {
                if (merchantId.HasValue)
                {
                    var permitList = db.tblPermits.Where(tp => tp.MerchantId.Equals(merchantId.Value)
                    && tp.tblSerialNumberShelf.SerialNumberType.Equals("IMP"))
                        .OrderByDescending(tp => tp.Date);
                    tblMerchant merchant = db.tblMerchants.Find(merchantId.Value);
                    ViewBag.ImporterName = merchant.ImporterName;
                    ViewBag.TinNumber = merchant.TinNumber;
                    ViewBag.NBENumber = merchant.NBENumber;
                    ViewBag.merchantId = merchant.Id;
                    return View(permitList.ToPagedList(numberOfPage, sizeOfPage));
                }
                else
                {
                    TempData["sErrMsg"] = "Enter Permit #.";
                    return RedirectToAction("Index", new RouteValueDictionary(new { permitNumber = permitNumber }));
                }
            }
            else
            {
                permitNumber = permitNumber.Trim();
                var permitCount = db.tblPermits.Where(tp => tp.PermitNumber.Contains(permitNumber)).Count();
                if (permitCount == 0)
                {
                    TempData["sErrMsg"] = "Permit # not found.";
                    return RedirectToAction("Index", new RouteValueDictionary(new { permitNumber = permitNumber }));
                }
                else if (permitCount != 1)
                {
                    TempData["sErrMsg"] = "Enter full Permit #.";
                    return RedirectToAction("Index", new RouteValueDictionary(new { permitNumber = permitNumber }));
                }
                else
                {
                    var permitList = db.tblPermits.Where(tp => tp.PermitNumber.Contains(permitNumber))
                        .Take(1).OrderByDescending(tp => tp.Date);
                    tblMerchant merchant = permitList.ToList().FirstOrDefault().tblMerchant;
                    ViewBag.ImporterName = merchant.ImporterName;
                    ViewBag.TinNumber = merchant.TinNumber;
                    ViewBag.NBENumber = merchant.NBENumber;
                    ViewBag.merchantId = merchant.Id;
                    ViewBag.permitNumber = permitNumber;
                    return View(permitList.ToPagedList(numberOfPage, sizeOfPage));
                }
            }
        }

        public tblPermit initMerchantFormPO(tblPermit permit)
        {
            List<string> currencyTypeList = new List<string>();
            //string date = DateTime.Now.DayOfWeek.ToString();
            //if (date == "Saturday")
            //{
            //    currencyTypeList = selectListOfCurrencyTypesSAT();
            //}
            //else if (date == "Sunday")
            //{
            //    currencyTypeList = selectListOfCurrencyTypesSUN();
            //}
            //else
            //{
            currencyTypeList = selectListOfCurrencyTypes();
            //}
            List<SelectListItem> currencyTypes = new List<SelectListItem>();
            foreach (string currencyType in currencyTypeList)
            {
                SelectListItem selectListItem = new SelectListItem();
                selectListItem.Text = currencyType;
                selectListItem.Value = currencyType;
                currencyTypes.Add(selectListItem);
            }
            ViewBag.CurrencyType = currencyTypes;
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")), "Id", "name", db.tbl_lu_Status
                .FirstOrDefault(tls => tls.name.Equals("Active")).Id);
            ViewBag.SelectedPortOfLoadingIds = new MultiSelectList(db.tbl_lu_PortOfLoading
                            .OrderBy(tlpol => tlpol.name), "Id", "name");
            ViewBag.SelectedPortOfDestinationIds = new MultiSelectList(db.tbl_lu_PortOfDestination
                            .OrderBy(tlpod => tlpod.name), "Id", "name");
            ViewBag.SelectedShipmentAllowedByIds = new MultiSelectList(db.tbl_lu_ShipmentAllowedBy
                            .OrderBy(tlsab => tlsab.name), "Id", "name");
            ViewBag.SelectedIncotermIds = new MultiSelectList(db.tbl_lu_Incoterm
                            .OrderBy(tli => tli.name), "Id", "name");
            ViewBag.SelectedCountryOfOriginIds = new MultiSelectList(db.tbl_lu_CountryOfOrigin
                            .OrderBy(tlc => tlc.name), "Id", "name");
            List<SelectListItem> approvalStatus = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NBE", Value = "NBE"
                },
                new SelectListItem {
                    Text = "Queue", Value = "Queue"
                },
                new SelectListItem {
                    Text = "Own Source", Value = "Own Source"
                },
                new SelectListItem {
                    Text = "President", Value = "President"
                },
                new SelectListItem {
                    Text = "On Demand", Value = "On Demand"
                }
            };
            ViewBag.ApprovalStatus = approvalStatus;
            List<SelectListItem> ownSourceValue = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NRFCY", Value = "NRFCY"
                },
                new SelectListItem {
                    Text = "Retention", Value = "Retention"
                },
                new SelectListItem {
                    Text = "Diaspora", Value = "Diaspora"
                },
            };
            ViewBag.OwnSourceValue = ownSourceValue;
            //####################################################################
            List<tblItemPriority> priorityList = db.tblItemPriorities.OrderBy(tip => tip.GroupBy).ToList();
            permit.firstPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("First Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            permit.secondPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Second Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            permit.thirdPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Third Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            permit.firstPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("First Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            permit.secondPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Second Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            permit.thirdPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Third Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            return permit;
        }
        public tblPermit initMerchantFormErrorPO(tblPermit permit)
        {
            List<string> currencyTypeList = new List<string>();
            //string date = DateTime.Now.DayOfWeek.ToString();
            //if (date == "Saturday")
            //{
            //    currencyTypeList = selectListOfCurrencyTypesSAT();
            //}
            //else if (date == "Sunday")
            //{
            //    currencyTypeList = selectListOfCurrencyTypesSUN();
            //}
            //else
            //{
            currencyTypeList = selectListOfCurrencyTypes();
            //}
            List<SelectListItem> currencyTypes = new List<SelectListItem>();
            foreach (string currencyType in currencyTypeList)
            {
                SelectListItem selectListItem = new SelectListItem();
                selectListItem.Text = currencyType;
                selectListItem.Value = currencyType;
                currencyTypes.Add(selectListItem);
            }
            ViewBag.CurrencyType = currencyTypes;
            //##
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")), "Id", "name", db.tbl_lu_Status
                .FirstOrDefault(tls => tls.name.Equals("Active")).Id);
            ViewBag.SelectedCountryOfOriginIds = new MultiSelectList(db.tbl_lu_CountryOfOrigin
                            .OrderBy(tlc => tlc.name), "Id", "name");
            ViewBag.SelectedPortOfLoadingIds = new MultiSelectList(db.tbl_lu_PortOfLoading
                            .OrderBy(tlpol => tlpol.name), "Id", "name");
            ViewBag.SelectedPortOfDestinationIds = new MultiSelectList(db.tbl_lu_PortOfDestination
                            .OrderBy(tlpod => tlpod.name), "Id", "name");
            ViewBag.SelectedShipmentAllowedByIds = new MultiSelectList(db.tbl_lu_ShipmentAllowedBy
                            .OrderBy(tlsab => tlsab.name), "Id", "name");
            ViewBag.SelectedIncotermIds = new MultiSelectList(db.tbl_lu_Incoterm
                            .OrderBy(tli => tli.name), "Id", "name");
            List<SelectListItem> approvalStatus = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NBE", Value = "NBE"
                },
                new SelectListItem {
                    Text = "Queue", Value = "Queue"
                },
                new SelectListItem {
                    Text = "Own Source", Value = "Own Source"
                },
                new SelectListItem {
                    Text = "President", Value = "President"
                },
                new SelectListItem {
                    Text = "On Demand", Value = "On Demand"
                }
            };
            ViewBag.ApprovalStatus = approvalStatus;
            List<SelectListItem> ownSourceValue = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NRFCY", Value = "NRFCY"
                },
                new SelectListItem {
                    Text = "Retention", Value = "Retention"
                },
                new SelectListItem {
                    Text = "Diaspora", Value = "Diaspora"
                },
            };
            ViewBag.OwnSourceValue = ownSourceValue;
            //####################################################################
            List<tblItemPriority> priorityList = db.tblItemPriorities.OrderBy(tip => tip.GroupBy).ToList();
            permit.firstPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("First Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.selectedFirstPriorityTopLevels.Contains(c.FirstOrDefault().GroupBy)
                }).ToList();
            permit.secondPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Second Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.selectedSecondPriorityTopLevels.Contains(c.FirstOrDefault().GroupBy)
                }).ToList();
            permit.thirdPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Third Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.selectedThirdPriorityTopLevels.Contains(c.FirstOrDefault().GroupBy)
                }).ToList();
            permit.firstPrioritySubLevels = priorityList
               .Where(pl => pl.Priority.Equals("First Priority")
               && permit.selectedFirstPriorityTopLevels.Contains(pl.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.selectedFirstPrioritySubLevels.Contains(c.GroupBy + "-" + c.Name)
               }).ToList();
            permit.secondPrioritySubLevels = priorityList
               .Where(pl => pl.Priority.Equals("Second Priority")
               && permit.selectedSecondPriorityTopLevels.Contains(pl.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.selectedSecondPrioritySubLevels.Contains(c.GroupBy + "-" + c.Name)
               }).ToList();
            permit.thirdPrioritySubLevels = priorityList
               .Where(pl => pl.Priority.Equals("Third Priority")
               && permit.selectedThirdPriorityTopLevels.Contains(pl.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.selectedThirdPrioritySubLevels.Contains(c.GroupBy + "-" + c.Name)
               }).ToList();
            return permit;
        }
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(tblMerchant merchant)
        {
            if (string.IsNullOrEmpty(merchant.TinNumber))
            {
                ModelState.AddModelError("TinNumber", "Required.");
            }
            if (string.IsNullOrEmpty(merchant.ImporterName))
            {
                ModelState.AddModelError("ImporterName", "Required.");
            }
            if (string.IsNullOrEmpty(merchant.NBENumber))
            {
                ModelState.AddModelError("NBENumber", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            bool tinNumberExists = dbe.tblMerchants
                                .Any(tm => tm.TinNumber.Equals(merchant.TinNumber));
                            if (tinNumberExists)
                            {
                                ModelState.AddModelError("TinNumber", "This Tin # is registered.");
                                return View(merchant);
                            }
                            bool NBENumberExists = dbe.tblMerchants
                                .Any(tm => tm.NBENumber.Equals(merchant.NBENumber));
                            if (NBENumberExists)
                            {
                                ModelState.AddModelError("NBENumber", "This NBE # is registered.");
                                return View(merchant);
                            }
                            tblMerchant _Merchant = new tblMerchant();
                            _Merchant.TinNumber = merchant.TinNumber.Trim();
                            _Merchant.ImporterName = merchant.ImporterName.Trim();
                            _Merchant.TradeName = merchant.TradeName;
                            _Merchant.NBENumber = merchant.NBENumber.Trim();
                            _Merchant.MobileNumber = merchant.MobileNumber;
                            _Merchant.EmailAddress = merchant.EmailAddress;
                            _Merchant.Remark = merchant.Remark;
                            dbe.tblMerchants.Add(_Merchant);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-Create";
                            string object_id = _Merchant.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Merchant created successfully!";
                            return RedirectToAction("Index");
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            return View(merchant);
        }
        public ActionResult CreatePurchaseOrderPermit(Guid Id)
        {
            tblPermit permit = new tblPermit();
            tblMerchant merchant = db.tblMerchants.Find(Id);
            permit.ImporterName = merchant.ImporterName;
            permit.TinNumber = merchant.TinNumber;
            permit.NBENumber = merchant.NBENumber;
            permit.MerchantId = merchant.Id;
            permit.ExpiryDays = "89";
            return View(initMerchantFormPO(permit));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreatePurchaseOrderPermit(tblPermit permit)
        {
            if (string.IsNullOrEmpty(permit.CurrencyRateValue)
            || permit.CurrencyRateValue.Equals("0"))
            {
                ModelState.AddModelError("CurrencyRateValue", "Required.");
            }
            if (string.IsNullOrEmpty(permit.AmountValue)
                || permit.AmountValue.Equals("0"))
            {
                ModelState.AddModelError("AmountValue", "Required.");
            }
            if (string.IsNullOrEmpty(permit.USDRateValue)
                || permit.USDRateValue.Equals("0"))
            {
                ModelState.AddModelError("USDRateValue", "Required.");
            }
            if (string.IsNullOrEmpty(permit.AmountInUSDValue)
                || permit.AmountInUSDValue.Equals("0"))
            {
                ModelState.AddModelError("AmountInUSDValue", "Required.");
            }
            if (string.IsNullOrEmpty(permit.AmountInBirrValue)
                || permit.AmountInBirrValue.Equals("0"))
            {
                ModelState.AddModelError("AmountInBirrValue", "Required.");
            }
            if (string.IsNullOrEmpty(permit.CurrencyType))
            {
                ModelState.AddModelError("CurrencyType", "Required.");
            }
            if (string.IsNullOrEmpty(permit.LPCONumber))
            {
                ModelState.AddModelError("LPCONumber", "Required.");
            }
            if (string.IsNullOrEmpty(permit.ExpiryDays))
            {
                ModelState.AddModelError("ExpiryDays", "Required.");
            }
            if (string.IsNullOrEmpty(permit.ApprovalStatus))
            {
                ModelState.AddModelError("ApprovalStatus", "Required.");
            }
            else
            {
                if (permit.ApprovalStatus.Equals("NBE"))
                {
                    if (string.IsNullOrEmpty(permit.NBEApprovalRefNumber))
                    {
                        ModelState.AddModelError("NBEApprovalRefNumber", "Required.");
                    }
                }
                else if (permit.ApprovalStatus.Equals("Queue"))
                {
                    if (string.IsNullOrEmpty(permit.QueueRound))
                    {
                        ModelState.AddModelError("QueueRound", "Required.");
                    }
                    if (string.IsNullOrEmpty(permit.QueueNumber))
                    {
                        ModelState.AddModelError("QueueNumber", "Required.");
                    }
                }
                else if (permit.ApprovalStatus.Equals("Own Source"))
                {
                    if (string.IsNullOrEmpty(permit.OwnSourceValue))
                    {
                        ModelState.AddModelError("OwnSourceValue", "Required.");
                    }
                }
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            string currentYear = selectCurrentYear();
                            tblPermit _Permit = new tblPermit();
                            _Permit.CreatedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                            _Permit.MerchantId = permit.MerchantId;
                            int nextSerialNumberValue = returnNextSerialNumberValueByType(dbe, "PO", currentYear);
                            tblSerialNumberShelf prevSerial = dbe.tblSerialNumberShelves
                                .FirstOrDefault(tsns => tsns.SerialNumberType.Equals("PO")
                                && tsns.IsLatest == true);
                            prevSerial.IsLatest = false;
                            dbe.SaveChanges();
                            tblSerialNumberShelf serialNumberShelf = new tblSerialNumberShelf();
                            serialNumberShelf.SerialNumberType = "PO";
                            serialNumberShelf.SerialNumberValue = nextSerialNumberValue;
                            serialNumberShelf.IsLatest = true;
                            serialNumberShelf.Year = currentYear;
                            dbe.tblSerialNumberShelves.Add(serialNumberShelf);
                            dbe.SaveChanges();
                            _Permit.LPCONumber = permit.LPCONumber;
                            _Permit.SerialNumberShelfId = serialNumberShelf.Id;
                            _Permit.PermitType = "06";
                            _Permit.PermitYear = currentYear;
                            _Permit.Date = DateTime.Now;
                            _Permit.CreatedDate = DateTime.Now;
                            _Permit.PermitStatusId = permit.PermitStatusId;
                            _Permit.PermitNumber = "ZB/TSP/"
                                + serialNumberShelf.SerialNumberValue.ToString().PadLeft(4, '0')
                                + "/" + _Permit.PermitYear;
                            _Permit.CurrencyType = permit.CurrencyType;
                            _Permit.CurrencyRate = formatDecimal(permit.CurrencyRateValue);
                            _Permit.Amount = formatDecimal(permit.AmountValue);
                            _Permit.RemainingAmount = _Permit.Amount;
                            _Permit.USDRate = formatDecimal(permit.USDRateValue);
                            _Permit.AmountInUSD = formatDecimal(permit.AmountInUSDValue);
                            _Permit.RemainingAmountInUSD = _Permit.AmountInUSD;
                            _Permit.AmountInBirr = formatDecimal(permit.AmountInBirrValue);
                            _Permit.RemainingAmountInBirr = _Permit.AmountInBirr;
                            _Permit.NonPriorityItems = permit.NonPriorityItems;
                            if (permit.ApprovalStatus.Equals("NBE"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.NBEApprovalRefNumber = permit.NBEApprovalRefNumber;
                            }
                            else if (permit.ApprovalStatus.Equals("Queue"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.QueueRound = permit.QueueRound;
                                _Permit.QueueNumber = permit.QueueNumber;
                            }
                            else if (permit.ApprovalStatus.Equals("Own Source"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.OwnSourceValue = permit.OwnSourceValue;
                            }
                            else if (permit.ApprovalStatus.Equals("President")
                                || permit.ApprovalStatus.Equals("On Demand"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                            }
                            dbe.tblPermits.Add(_Permit);
                            List<tbl_lu_PortOfLoading> portOfLoadings = dbe.tbl_lu_PortOfLoading.ToList();
                            List<tbl_lu_PortOfDestination> portOfDestinations = dbe.tbl_lu_PortOfDestination.ToList();
                            List<tbl_lu_ShipmentAllowedBy> shipmentAllowedBies = dbe.tbl_lu_ShipmentAllowedBy.ToList();
                            List<tbl_lu_Incoterm> incoterms = dbe.tbl_lu_Incoterm.ToList();
                            List<tbl_lu_CountryOfOrigin> countryOfOrigins = dbe.tbl_lu_CountryOfOrigin.ToList();
                            if (permit.SelectedPortOfLoadingIds != null)
                            {
                                foreach (Guid Id in permit.SelectedPortOfLoadingIds)
                                {
                                    _Permit.tbl_lu_PortOfLoading.Add(portOfLoadings.FirstOrDefault(pol => pol.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedPortOfDestinationIds != null)
                            {
                                foreach (Guid Id in permit.SelectedPortOfDestinationIds)
                                {
                                    _Permit.tbl_lu_PortOfDestination.Add(portOfDestinations.FirstOrDefault(pod => pod.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedShipmentAllowedByIds != null)
                            {
                                foreach (Guid Id in permit.SelectedShipmentAllowedByIds)
                                {
                                    _Permit.tbl_lu_ShipmentAllowedBy.Add(shipmentAllowedBies.FirstOrDefault(sab => sab.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedIncotermIds != null)
                            {
                                foreach (Guid Id in permit.SelectedIncotermIds)
                                {
                                    _Permit.tbl_lu_Incoterm.Add(incoterms.FirstOrDefault(i => i.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedCountryOfOriginIds != null)
                            {
                                foreach (Guid Id in permit.SelectedCountryOfOriginIds)
                                {
                                    _Permit.tbl_lu_CountryOfOrigin.Add(countryOfOrigins.FirstOrDefault(coo => coo.Id.Equals(Id)));
                                }
                            }
                            List<tblItemPriority> itemPriorities = dbe.tblItemPriorities.ToList();
                            if (permit.selectedFirstPrioritySubLevels != null)
                            {
                                foreach (string selectedFirstPriorityItem in permit.selectedFirstPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("First Priority")
                                            && groupByName.Equals(selectedFirstPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            if (permit.selectedSecondPrioritySubLevels != null)
                            {
                                foreach (string selectedSecondPriorityItem in permit.selectedSecondPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("Second Priority")
                                            && groupByName.Equals(selectedSecondPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            if (permit.selectedThirdPrioritySubLevels != null)
                            {
                                foreach (string selectedThirdPriorityItem in permit.selectedThirdPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("Third Priority")
                                            && groupByName.Equals(selectedThirdPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            tblPOPermitExpiry pOPermitExpiry = new tblPOPermitExpiry();
                            pOPermitExpiry.PermitId = _Permit.Id;
                            pOPermitExpiry.ExpiryDate = _Permit.Date.Value.AddDays(int.Parse(permit.ExpiryDays));
                            pOPermitExpiry.IsExtension = false;
                            dbe.tblPOPermitExpiries.Add(pOPermitExpiry);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-CreatePurchaseOrderPermit";
                            string object_id = _Permit.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Purchase order permit successfully created!";
                            return RedirectToAction("PurchaseOrderPermitConfirmation", new RouteValueDictionary(new { Id = _Permit.Id }));
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            return View(initMerchantFormErrorPO(permit));
        }

        public JsonResult NameBySubLevel(string permitId, string[] subLevels)
        {
            string result = "";
            List<SelectListItem> nameList = new List<SelectListItem>();
            try
            {
                if (subLevels != null)
                {
                    if (permitId != null)
                    {
                        List<tblItemPriority> priorityList = db.tblItemPriorities.OrderBy(tip => tip.GroupBy).ToList();
                        tblPermit permit = db.tblPermits.Find(Guid.Parse(permitId));
                        nameList = priorityList
                            .Where(tip => subLevels.Contains(tip.GroupBy))
                            .OrderBy(tip => tip.GroupBy)
                            .Select(c => new SelectListItem()
                            {
                                Text = string.IsNullOrEmpty(c.Name)
                                ? c.GroupBy : c.GroupBy + "-" + c.Name,
                                Value = c.GroupBy + "-" + c.Name,
                                Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
                            }).ToList();
                    }
                    else
                    {
                        nameList = db.tblItemPriorities
                            .Where(tip => subLevels.Contains(tip.GroupBy))
                            .OrderBy(tip => tip.GroupBy)
                            .Select(c => new SelectListItem()
                            {
                                Text = string.IsNullOrEmpty(c.Name)
                                ? c.GroupBy : c.GroupBy + "-" + c.Name,
                                Value = c.GroupBy + "-" + c.Name
                            }).ToList();
                    }
                    JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
                    result = javaScriptSerializer.Serialize(nameList);
                }
            }
            catch (Exception) { }
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public JsonResult LoadCreateImportPermit(Guid[] permitIds, string amount)
        {
            string result = "";
            PermitDetails permitDetails = new PermitDetails();
            try
            {
                amount = string.IsNullOrEmpty(amount) ? "0" : amount;
                if (permitIds != null)
                {
                    decimal _Amount = decimal.Parse(amount);
                    permitDetails.PortOfLoadingIds = db.tbl_lu_PortOfLoading
                        .Where(tlpol => tlpol.tblPermits.Any(tp => permitIds.Contains(tp.Id)))
                        .ToList().Select(tlpol => tlpol.Id.ToString())
                        .ToList();
                    permitDetails.PortOfDestinationIds = db.tbl_lu_PortOfDestination
                        .Where(tlpod => tlpod.tblPermits.Any(tp => permitIds.Contains(tp.Id)))
                        .ToList().Select(tlpod => tlpod.Id.ToString())
                        .ToList();
                    permitDetails.ShipmentAllowedByIds = db.tbl_lu_ShipmentAllowedBy
                        .Where(tlsab => tlsab.tblPermits.Any(tp => permitIds.Contains(tp.Id)))
                        .ToList().Select(tlsab => tlsab.Id.ToString())
                        .ToList();
                    permitDetails.IncotermIds = db.tbl_lu_Incoterm
                        .Where(tli => tli.tblPermits.Any(tp => permitIds.Contains(tp.Id)))
                        .ToList().Select(tli => tli.Id.ToString())
                        .ToList();
                    List<tblPermit> permitList = db.tblPermits.Where(tp => permitIds.Contains(tp.Id)).ToList();
                    permitDetails.CurrencyType = permitList.FirstOrDefault().CurrencyType;
                    permitDetails.ApprovalStatus = permitList.FirstOrDefault().ApprovalStatus;
                    permitDetails.NBEApprovalRefNumber = permitList.FirstOrDefault().NBEApprovalRefNumber;
                    permitDetails.OwnSourceValue = permitList.FirstOrDefault().OwnSourceValue;
                    permitDetails.QueueRound = permitList.FirstOrDefault().QueueRound;
                    permitDetails.QueueNumber = permitList.FirstOrDefault().QueueNumber;
                    permitDetails.CountryOfOriginIds = db.tbl_lu_CountryOfOrigin
                        .Where(tlcoo => tlcoo.tblPermits.Any(tp => permitIds.Contains(tp.Id)))
                        .ToList().Select(tlcoo => tlcoo.Id.ToString())
                        .ToList();
                    //****************************************************
                    List<tblItemPriority> priorityList = db.tblItemPriorities.OrderBy(tip => tip.GroupBy).ToList();
                    permitDetails.FirstPriorityTopLevels = priorityList
                        .Where(tip => tip.Priority.Equals("First Priority"))
                        .GroupBy(tip => tip.GroupBy)
                        .Select(c => new MultiSelectOption()
                        {
                            label = c.FirstOrDefault().GroupBy,
                            title = c.FirstOrDefault().GroupBy,
                            value = c.FirstOrDefault().GroupBy,
                            selected = c.Any(tip => tip.tblPermits.Any(tp => permitIds.Contains(tp.Id)))
                        }).ToList();
                    permitDetails.FirstPrioritySubLevels = priorityList
                        .Where(tip => permitDetails.FirstPriorityTopLevels.Any(fptl => fptl.label.Equals(tip.GroupBy)))
                        .OrderBy(tip => tip.GroupBy)
                        .Select(c => new SelectListItem()
                        {
                            Text = string.IsNullOrEmpty(c.Name)
                            ? c.GroupBy : c.GroupBy + "-" + c.Name,
                            Value = c.GroupBy + "-" + c.Name,
                            Selected = c.tblPermits.Any(tp => permitIds.Contains(tp.Id))
                        }).ToList();
                    permitDetails.SecondPriorityTopLevels = priorityList
                        .Where(tip => tip.Priority.Equals("Second Priority"))
                        .GroupBy(tip => tip.GroupBy)
                        .Select(c => new MultiSelectOption()
                        {
                            label = c.FirstOrDefault().GroupBy,
                            title = c.FirstOrDefault().GroupBy,
                            value = c.FirstOrDefault().GroupBy,
                            selected = c.Any(tip => tip.tblPermits.Any(tp => permitIds.Contains(tp.Id)))
                        }).ToList();
                    permitDetails.SecondPrioritySubLevels = priorityList
                        .Where(tip => permitDetails.SecondPriorityTopLevels.Any(fptl => fptl.label.Equals(tip.GroupBy)))
                        .OrderBy(tip => tip.GroupBy)
                        .Select(c => new SelectListItem()
                        {
                            Text = string.IsNullOrEmpty(c.Name)
                            ? c.GroupBy : c.GroupBy + "-" + c.Name,
                            Value = c.GroupBy + "-" + c.Name,
                            Selected = c.tblPermits.Any(tp => permitIds.Contains(tp.Id))
                        }).ToList();
                    permitDetails.ThirdPriorityTopLevels = priorityList
                        .Where(tip => tip.Priority.Equals("Third Priority"))
                        .GroupBy(tip => tip.GroupBy)
                        .Select(c => new MultiSelectOption()
                        {
                            label = c.FirstOrDefault().GroupBy,
                            title = c.FirstOrDefault().GroupBy,
                            value = c.FirstOrDefault().GroupBy,
                            selected = c.Any(tip => tip.tblPermits.Any(tp => permitIds.Contains(tp.Id)))
                        }).ToList();
                    permitDetails.ThirdPrioritySubLevels = priorityList
                        .Where(tip => permitDetails.ThirdPriorityTopLevels.Any(fptl => fptl.label.Equals(tip.GroupBy)))
                        .OrderBy(tip => tip.GroupBy)
                        .Select(c => new SelectListItem()
                        {
                            Text = string.IsNullOrEmpty(c.Name)
                            ? c.GroupBy : c.GroupBy + "-" + c.Name,
                            Value = c.GroupBy + "-" + c.Name,
                            Selected = c.tblPermits.Any(tp => permitIds.Contains(tp.Id))
                        }).ToList();
                    List<string> nonPriorityItemList = permitList.Select(tp => tp.NonPriorityItems)
                        .ToList();
                    permitDetails.NonPriorityItems = string.Join(", ", nonPriorityItemList);
                    permitDetails.PermitNumbers = permitList.Select(tp => tp.PermitNumber).ToList();
                    CurrencyDetails currencyDetails = new CurrencyDetails();
                    try
                    {
                        currencyDetails = returnCurrencyDetails(permitDetails.CurrencyType, _Amount);
                    }
                    catch (Exception ex) { }
                    permitDetails.CurrencyRate = currencyDetails.CurrencyRate;
                    permitDetails.AmountInBirr = currencyDetails.AmountInBirr;
                    permitDetails.USDRate = currencyDetails.USDRate;
                    permitDetails.AmountInUSD = currencyDetails.AmountInUSD;
                    //****************************************************
                    JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
                    result = javaScriptSerializer.Serialize(permitDetails);
                }
            }
            catch (Exception ex) { }
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public tblPermit initEditPurchaseOrderPermitForm(tblPermit permit)
        {
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")), "Id", "name", permit.PermitStatusId);
            ViewBag.SelectedPortOfLoadingIds = new MultiSelectList(db.tbl_lu_PortOfLoading
                            .OrderBy(tlpol => tlpol.name), "Id", "name", permit.tbl_lu_PortOfLoading.Select(tlpol => tlpol.Id));
            ViewBag.SelectedPortOfDestinationIds = new MultiSelectList(db.tbl_lu_PortOfDestination
                            .OrderBy(tlpod => tlpod.name), "Id", "name", permit.tbl_lu_PortOfDestination.Select(tlpod => tlpod.Id));
            ViewBag.SelectedShipmentAllowedByIds = new MultiSelectList(db.tbl_lu_ShipmentAllowedBy
                            .OrderBy(tlsab => tlsab.name), "Id", "name", permit.tbl_lu_ShipmentAllowedBy.Select(tlsab => tlsab.Id));
            ViewBag.SelectedIncotermIds = new MultiSelectList(db.tbl_lu_Incoterm
                            .OrderBy(tli => tli.name), "Id", "name", permit.tbl_lu_Incoterm.Select(tli => tli.Id));
            ViewBag.SelectedCountryOfOriginIds = new MultiSelectList(db.tbl_lu_CountryOfOrigin
                            .OrderBy(tlc => tlc.name), "Id", "name", permit.tbl_lu_CountryOfOrigin.Select(tlcoo => tlcoo.Id));
            List<SelectListItem> approvalStatus = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NBE", Value = "NBE", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("NBE")
                },
                new SelectListItem {
                    Text = "Queue", Value = "Queue", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("Queue")
                },
                new SelectListItem {
                    Text = "Own Source", Value = "Own Source", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("Own Source")
                },
                new SelectListItem {
                    Text = "President", Value = "President", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("President")
                },
                new SelectListItem {
                    Text = "On Demand", Value = "On Demand", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("On Demand")
                }
            };
            ViewBag.ApprovalStatus = approvalStatus;
            List<SelectListItem> ownSourceValue = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NRFCY", Value = "NRFCY", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("NRFCY")
                },
                new SelectListItem {
                    Text = "Retention", Value = "Retention", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("Retention")
                },
                new SelectListItem {
                    Text = "Diaspora", Value = "Diaspora", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("Diaspora")
                },
            };
            ViewBag.OwnSourceValue = ownSourceValue;
            DateTime expiryDate = permit.tblPOPermitExpiries.FirstOrDefault(tppe => tppe.IsExtension == false).ExpiryDate;
            DateTime permitDate = permit.Date.Value.Date;
            permit.ExpiryDays = (expiryDate - permitDate).Days.ToString();
            List<tblItemPriority> priorityList = db.tblItemPriorities.OrderBy(tip => tip.GroupBy).ToList();
            permit.firstPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("First Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.firstPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedFirstPriorityItem in permit.firstPriorityTopLevels)
                {
                    if (selectedFirstPriorityItem.Selected)
                    {
                        permit.selectedFirstPriorityTopLevels.Add(selectedFirstPriorityItem.Value);
                    }
                }
            }
            permit.secondPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Second Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.secondPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedSecondPriorityItem in permit.secondPriorityTopLevels)
                {
                    if (selectedSecondPriorityItem.Selected)
                    {
                        permit.selectedSecondPriorityTopLevels.Add(selectedSecondPriorityItem.Value);
                    }
                }
            }
            permit.thirdPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Third Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.thirdPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedThirdPriorityItem in permit.thirdPriorityTopLevels)
                {
                    if (selectedThirdPriorityItem.Selected)
                    {
                        permit.selectedThirdPriorityTopLevels.Add(selectedThirdPriorityItem.Value);
                    }
                }
            }
            permit.firstPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("First Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.firstPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedFirstPriorityItem in permit.firstPrioritySubLevels)
                {
                    if (selectedFirstPriorityItem.Selected)
                    {
                        permit.selectedFirstPrioritySubLevels.Add(selectedFirstPriorityItem.Value);
                    }
                }
            }
            permit.secondPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Second Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.secondPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedSecondPriorityItem in permit.secondPrioritySubLevels)
                {
                    if (selectedSecondPriorityItem.Selected)
                    {
                        permit.selectedSecondPrioritySubLevels.Add(selectedSecondPriorityItem.Value);
                    }
                }
            }
            permit.thirdPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Third Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.thirdPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedThirdPriorityItem in permit.thirdPrioritySubLevels)
                {
                    if (selectedThirdPriorityItem.Selected)
                    {
                        permit.selectedThirdPrioritySubLevels.Add(selectedThirdPriorityItem.Value);
                    }
                }
            }
            return permit;
        }

        public tblPermit initEditPurchaseOrderPermitFormError(tblPermit permit)
        {
            permit = db.tblPermits.Find(permit.Id);
            permit.ImporterName = permit.tblMerchant.ImporterName;
            permit.TinNumber = permit.tblMerchant.TinNumber;
            permit.NBENumber = permit.tblMerchant.NBENumber;
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")), "Id", "name", permit.PermitStatusId);
            ViewBag.MethodOfPaymentId = new SelectList(db.tbl_lu_MethodOfPayment
                .OrderBy(tlmp => tlmp.name), "Id", "name", permit.MethodOfPaymentId);
            ViewBag.SelectedPortOfLoadingIds = new MultiSelectList(db.tbl_lu_PortOfLoading
                            .OrderBy(tlpol => tlpol.name), "Id", "name", permit.tbl_lu_PortOfLoading.Select(tlpol => tlpol.Id));
            ViewBag.SelectedPortOfDestinationIds = new MultiSelectList(db.tbl_lu_PortOfDestination
                            .OrderBy(tlpod => tlpod.name), "Id", "name", permit.tbl_lu_PortOfDestination.Select(tlpod => tlpod.Id));
            ViewBag.SelectedShipmentAllowedByIds = new MultiSelectList(db.tbl_lu_ShipmentAllowedBy
                            .OrderBy(tlsab => tlsab.name), "Id", "name", permit.tbl_lu_ShipmentAllowedBy.Select(tlsab => tlsab.Id));
            ViewBag.SelectedIncotermIds = new MultiSelectList(db.tbl_lu_Incoterm
                            .OrderBy(tli => tli.name), "Id", "name", permit.tbl_lu_Incoterm.Select(tli => tli.Id));
            ViewBag.SelectedCountryOfOriginIds = new MultiSelectList(db.tbl_lu_CountryOfOrigin
                            .OrderBy(tlc => tlc.name), "Id", "name", permit.tbl_lu_CountryOfOrigin.Select(tlcoo => tlcoo.Id));
            List<SelectListItem> approvalStatus = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NBE", Value = "NBE", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("NBE")
                },
                new SelectListItem {
                    Text = "Queue", Value = "Queue", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("Queue")
                },
                new SelectListItem {
                    Text = "Own Source", Value = "Own Source", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("Own Source")
                },
                new SelectListItem {
                    Text = "President", Value = "President", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("President")
                },
                new SelectListItem {
                    Text = "On Demand", Value = "On Demand", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("On Demand")
                }
            };
            ViewBag.ApprovalStatus = approvalStatus;
            List<SelectListItem> ownSourceValue = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NRFCY", Value = "NRFCY", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("NRFCY")
                },
                new SelectListItem {
                    Text = "Retention", Value = "Retention", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("Retention")
                },
                new SelectListItem {
                    Text = "Diaspora", Value = "Diaspora", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("Diaspora")
                },
            };
            ViewBag.OwnSourceValue = ownSourceValue;
            DateTime expiryDate = permit.tblPOPermitExpiries.FirstOrDefault(tppe => tppe.IsExtension == false).ExpiryDate;
            DateTime permitDate = permit.Date.Value.Date;
            permit.ExpiryDays = (expiryDate - permitDate).Days.ToString();
            List<tblItemPriority> priorityList = db.tblItemPriorities.OrderBy(tip => tip.GroupBy).ToList();
            permit.firstPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("First Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.firstPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedFirstPriorityItem in permit.firstPriorityTopLevels)
                {
                    if (selectedFirstPriorityItem.Selected)
                    {
                        permit.selectedFirstPriorityTopLevels.Add(selectedFirstPriorityItem.Value);
                    }
                }
            }
            permit.secondPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Second Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.secondPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedSecondPriorityItem in permit.secondPriorityTopLevels)
                {
                    if (selectedSecondPriorityItem.Selected)
                    {
                        permit.selectedSecondPriorityTopLevels.Add(selectedSecondPriorityItem.Value);
                    }
                }
            }
            permit.thirdPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Third Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.thirdPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedThirdPriorityItem in permit.thirdPriorityTopLevels)
                {
                    if (selectedThirdPriorityItem.Selected)
                    {
                        permit.selectedThirdPriorityTopLevels.Add(selectedThirdPriorityItem.Value);
                    }
                }
            }
            permit.firstPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("First Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.firstPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedFirstPriorityItem in permit.firstPrioritySubLevels)
                {
                    if (selectedFirstPriorityItem.Selected)
                    {
                        permit.selectedFirstPrioritySubLevels.Add(selectedFirstPriorityItem.Value);
                    }
                }
            }
            permit.secondPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Second Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.secondPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedSecondPriorityItem in permit.secondPrioritySubLevels)
                {
                    if (selectedSecondPriorityItem.Selected)
                    {
                        permit.selectedSecondPrioritySubLevels.Add(selectedSecondPriorityItem.Value);
                    }
                }
            }
            permit.thirdPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Third Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.thirdPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedThirdPriorityItem in permit.thirdPrioritySubLevels)
                {
                    if (selectedThirdPriorityItem.Selected)
                    {
                        permit.selectedThirdPrioritySubLevels.Add(selectedThirdPriorityItem.Value);
                    }
                }
            }
            return permit;
        }

        public ActionResult EditPurchaseOrderPermit(Guid Id)
        {
            tblPermit permit = db.tblPermits.Find(Id);
            permit.ImporterName = permit.tblMerchant.ImporterName;
            permit.TinNumber = permit.tblMerchant.TinNumber;
            permit.NBENumber = permit.tblMerchant.NBENumber;
            if (!string.IsNullOrEmpty(permit.ApprovalStatus))
            {
                if (permit.ApprovalStatus.Equals("NBE"))
                {
                    permit.NBEApprovalRefNumber = permit.NBEApprovalRefNumber;
                }
                else if (permit.ApprovalStatus.Equals("Queue"))
                {
                    permit.QueueRound = permit.QueueRound;
                    permit.QueueNumber = permit.QueueNumber;
                }
                else if (permit.ApprovalStatus.Equals("Own Source"))
                {
                    permit.OwnSourceValue = permit.OwnSourceValue;
                }
                else if (permit.ApprovalStatus.Equals("President")
                    || permit.ApprovalStatus.Equals("On Demand"))
                {
                    permit.ApprovalStatus = permit.ApprovalStatus;
                }
            }
            return View(initEditPurchaseOrderPermitForm(permit));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPurchaseOrderPermit(tblPermit permit)
        {
            if (string.IsNullOrEmpty(permit.LPCONumber))
            {
                ModelState.AddModelError("LPCONumber", "Required.");
            }
            if (string.IsNullOrEmpty(permit.ExpiryDays))
            {
                ModelState.AddModelError("ExpiryDays", "Required.");
            }
            if (string.IsNullOrEmpty(permit.ApprovalStatus))
            {
                ModelState.AddModelError("ApprovalStatus", "Required.");
            }
            else
            {
                if (permit.ApprovalStatus.Equals("NBE"))
                {
                    if (string.IsNullOrEmpty(permit.NBEApprovalRefNumber))
                    {
                        ModelState.AddModelError("NBEApprovalRefNumber", "Required.");
                    }
                }
                else if (permit.ApprovalStatus.Equals("Queue"))
                {
                    if (string.IsNullOrEmpty(permit.QueueRound))
                    {
                        ModelState.AddModelError("QueueRound", "Required.");
                    }
                    if (string.IsNullOrEmpty(permit.QueueNumber))
                    {
                        ModelState.AddModelError("QueueNumber", "Required.");
                    }
                }
                else if (permit.ApprovalStatus.Equals("Own Source"))
                {
                    if (string.IsNullOrEmpty(permit.OwnSourceValue))
                    {
                        ModelState.AddModelError("OwnSourceValue", "Required.");
                    }
                }
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            tblPermit _Permit = dbe.tblPermits.Find(permit.Id);
                            _Permit.MethodOfPaymentId = permit.MethodOfPaymentId;
                            _Permit.LPCONumber = permit.LPCONumber;
                            _Permit.PermitStatusId = permit.PermitStatusId;
                            //clear approval status
                            _Permit.ApprovalStatus = "";
                            _Permit.NBEApprovalRefNumber = "";
                            _Permit.ApprovalStatus = "";
                            _Permit.QueueRound = "";
                            _Permit.QueueNumber = "";
                            _Permit.ApprovalStatus = "";
                            _Permit.OwnSourceValue = "";
                            _Permit.ApprovalStatus = "";
                            //
                            if (permit.ApprovalStatus.Equals("NBE"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.NBEApprovalRefNumber = permit.NBEApprovalRefNumber;
                            }
                            else if (permit.ApprovalStatus.Equals("Queue"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.QueueRound = permit.QueueRound;
                                _Permit.QueueNumber = permit.QueueNumber;
                            }
                            else if (permit.ApprovalStatus.Equals("Own Source"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.OwnSourceValue = permit.OwnSourceValue;
                            }
                            else if (permit.ApprovalStatus.Equals("President")
                                || permit.ApprovalStatus.Equals("On Demand"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                            }
                            _Permit.tblItemPriorities.Clear();
                            _Permit.tbl_lu_PortOfLoading.Clear();
                            _Permit.tbl_lu_PortOfDestination.Clear();
                            _Permit.tbl_lu_ShipmentAllowedBy.Clear();
                            _Permit.tbl_lu_Incoterm.Clear();
                            _Permit.tbl_lu_CountryOfOrigin.Clear();
                            _Permit.NonPriorityItems = permit.NonPriorityItems;
                            List<tblItemPriority> itemPriorities = dbe.tblItemPriorities.ToList();
                            if (permit.selectedFirstPrioritySubLevels != null)
                            {
                                foreach (string selectedFirstPriorityItem in permit.selectedFirstPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("First Priority")
                                            && groupByName.Equals(selectedFirstPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            if (permit.selectedSecondPrioritySubLevels != null)
                            {
                                foreach (string selectedSecondPriorityItem in permit.selectedSecondPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("Second Priority")
                                            && groupByName.Equals(selectedSecondPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            if (permit.selectedThirdPrioritySubLevels != null)
                            {
                                foreach (string selectedThirdPriorityItem in permit.selectedThirdPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("Third Priority")
                                            && groupByName.Equals(selectedThirdPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            tblPOPermitExpiry pOPermitExpiry = _Permit.tblPOPermitExpiries.FirstOrDefault(tppe => tppe.IsExtension == false);
                            pOPermitExpiry.ExpiryDate = _Permit.Date.Value.AddDays(int.Parse(permit.ExpiryDays));
                            List<tbl_lu_PortOfLoading> portOfLoadings = dbe.tbl_lu_PortOfLoading.ToList();
                            List<tbl_lu_PortOfDestination> portOfDestinations = dbe.tbl_lu_PortOfDestination.ToList();
                            List<tbl_lu_ShipmentAllowedBy> shipmentAllowedBies = dbe.tbl_lu_ShipmentAllowedBy.ToList();
                            List<tbl_lu_Incoterm> incoterms = dbe.tbl_lu_Incoterm.ToList();
                            List<tbl_lu_CountryOfOrigin> countryOfOrigins = dbe.tbl_lu_CountryOfOrigin.ToList();
                            if (permit.SelectedPortOfLoadingIds != null)
                            {
                                foreach (Guid Id in permit.SelectedPortOfLoadingIds)
                                {
                                    _Permit.tbl_lu_PortOfLoading.Add(portOfLoadings.FirstOrDefault(pol => pol.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedPortOfDestinationIds != null)
                            {
                                foreach (Guid Id in permit.SelectedPortOfDestinationIds)
                                {
                                    _Permit.tbl_lu_PortOfDestination.Add(portOfDestinations.FirstOrDefault(pod => pod.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedShipmentAllowedByIds != null)
                            {
                                foreach (Guid Id in permit.SelectedShipmentAllowedByIds)
                                {
                                    _Permit.tbl_lu_ShipmentAllowedBy.Add(shipmentAllowedBies.FirstOrDefault(sab => sab.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedIncotermIds != null)
                            {
                                foreach (Guid Id in permit.SelectedIncotermIds)
                                {
                                    _Permit.tbl_lu_Incoterm.Add(incoterms.FirstOrDefault(i => i.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedCountryOfOriginIds != null)
                            {
                                foreach (Guid Id in permit.SelectedCountryOfOriginIds)
                                {
                                    _Permit.tbl_lu_CountryOfOrigin.Add(countryOfOrigins.FirstOrDefault(coo => coo.Id.Equals(Id)));
                                }
                            }
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-EditPurchaseOrderPermit";
                            string object_id = _Permit.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Purchase order permit successfully edited!";
                            return RedirectToAction("PurchaseOrderPermits", new RouteValueDictionary(new { merchantId = _Permit.MerchantId }));
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            return View(initEditPurchaseOrderPermitFormError(permit));
        }

        public ActionResult Edit(Guid? Id)
        {
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            tblMerchant merchant = db.tblMerchants.Find(Id);
            merchant.OldTinNumber = merchant.TinNumber;
            merchant.OldNBENumber = merchant.NBENumber;
            if (merchant == null)
            {
                return HttpNotFound();
            }
            return View(merchant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(tblMerchant merchant)
        {
            if (string.IsNullOrEmpty(merchant.TinNumber))
            {
                ModelState.AddModelError("TinNumber", "Required.");
            }
            if (string.IsNullOrEmpty(merchant.ImporterName))
            {
                ModelState.AddModelError("ImporterName", "Required.");
            }
            if (string.IsNullOrEmpty(merchant.NBENumber))
            {
                ModelState.AddModelError("NBENumber", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            if (!merchant.OldTinNumber.Equals(merchant.TinNumber.Trim()))
                            {
                                bool tinNumberExists = dbe.tblMerchants
                                .Any(tm => tm.TinNumber.Equals(merchant.TinNumber.Trim()));
                                if (tinNumberExists)
                                {
                                    ModelState.AddModelError("TinNumber", "This Tin # is registered.");
                                    return View(merchant);
                                }
                            }
                            if (!merchant.OldNBENumber.Equals(merchant.NBENumber.Trim()))
                            {
                                bool NBENumberExists = dbe.tblMerchants
                                .Any(tm => tm.NBENumber.Equals(merchant.NBENumber.Trim()));
                                if (NBENumberExists)
                                {
                                    ModelState.AddModelError("NBENumber", "This NBE # is registered.");
                                    return View(merchant);
                                }
                            }
                            tblMerchant _Merchant = dbe.tblMerchants.Find(merchant.Id);
                            _Merchant.TinNumber = merchant.TinNumber.Trim();
                            _Merchant.ImporterName = merchant.ImporterName;
                            _Merchant.TradeName = merchant.TradeName;
                            _Merchant.NBENumber = merchant.NBENumber.Trim();
                            _Merchant.MobileNumber = merchant.MobileNumber;
                            _Merchant.EmailAddress = merchant.EmailAddress;
                            _Merchant.Remark = merchant.Remark;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-Edit";
                            string object_id = _Merchant.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Merchant successfully edited!";
                            return RedirectToAction("Index");
                        }
                        catch (Exception ex)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            return View(merchant);
        }
        public ActionResult ViewMerchantDetail(Guid Id)
        {
            tblMerchant merchant = db.tblMerchants.Find(Id);
            return PartialView(merchant);
        }
        public ActionResult PurchaseOrderPermitConfirmation(Guid Id)
        {
            tblPermit permit = db.tblPermits.FirstOrDefault(tp => tp.Id.Equals(Id));
            foreach (tbl_lu_CountryOfOrigin countryOfOrigin in permit.tbl_lu_CountryOfOrigin)
            {
                if (string.IsNullOrEmpty(permit.CountryOfOriginNames))
                {
                    permit.CountryOfOriginNames += countryOfOrigin.name;
                }
                else
                {
                    permit.CountryOfOriginNames += ", " + countryOfOrigin.name;
                }
            }
            if (permit.ApprovalStatus.Equals("NBE"))
            {
                permit.NBEApprovalRefNumber = permit.NBEApprovalRefNumber;
            }
            else if (permit.ApprovalStatus.Equals("Queue"))
            {
                permit.QueueRound = permit.QueueRound;
                permit.QueueNumber = permit.QueueNumber;
            }
            else if (permit.ApprovalStatus.Equals("Own Source"))
            {
                permit.OwnSourceValue = permit.OwnSourceValue;
            }
            else if (permit.ApprovalStatus.Equals("President")
                || permit.ApprovalStatus.Equals("On Demand"))
            {
                permit.ApprovalStatus = permit.ApprovalStatus;
            }
            permit.ExpiryDays = permit.tblPOPermitExpiries
                .OrderByDescending(tppe => tppe.ExpiryDate).FirstOrDefault()
                .ExpiryDate.ToString("D");
            return View(permit);
        }

        public ActionResult ImportPermitConfirmation(Guid Id)
        {
            tblPermit permit = db.tblPermits.FirstOrDefault(tp => tp.Id.Equals(Id));
            foreach (tbl_lu_CountryOfOrigin countryOfOrigin in permit.tbl_lu_CountryOfOrigin)
            {
                if (string.IsNullOrEmpty(permit.CountryOfOriginNames))
                {
                    permit.CountryOfOriginNames += countryOfOrigin.name;
                }
                else
                {
                    permit.CountryOfOriginNames += ", " + countryOfOrigin.name;
                }
            }
            //Some import permits do not have expiries, so check.
            bool expiryExists = permit.tblPOPermitExpiries.Any();
            if (expiryExists)
            {
                permit.ExpiryDate = permit.tblPOPermitExpiries.OrderByDescending(tpope => tpope.ExpiryDate).FirstOrDefault().ExpiryDate;
            }
            return View(permit);
        }

        public ActionResult ViewPermitDetail(Guid Id)
        {
            tblPermit permit = db.tblPermits.Find(Id);
            return PartialView(permit);
        }

        public ActionResult ViewPODetail(Guid Id)
        {
            tblPermit permit = db.tblPermits.Find(Id);
            return PartialView(permit);
        }

        public ActionResult ViewPOPermitAmountUpdateRejectionComment(Guid Id)
        {
            tblPermitAmount permitAmount = db.tblPermitAmounts.Find(Id);
            return PartialView(permitAmount);
        }

        public ActionResult ViewImportPermitAmountUpdateRejectionComment(Guid Id)
        {
            tblPermitAmount permitAmount = db.tblPermitAmounts.Find(Id);
            return PartialView(permitAmount);
        }

        public ActionResult ViewPurchaseOrderPermitDetail(Guid Id)
        {
            tblPermitAmount permitAmount = db.tblPermitAmounts
                        .FirstOrDefault(tpa => tpa.Id.Equals(Id));
            return PartialView(permitAmount);
        }

        public ActionResult ViewImportPermitDetail(Guid Id)
        {
            tblPermitAmount permitAmount = db.tblPermitAmounts
                        .FirstOrDefault(tpa => tpa.Id.Equals(Id));
            return PartialView(permitAmount);
        }
        public ActionResult CreatePOPermitExpiry(Guid Id)
        {
            tblPermit permit = db.tblPermits.Find(Id);
            return View(permit);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreatePOPermitExpiry(tblPermit permit)
        {
            if (string.IsNullOrEmpty(permit.ExpiryDays))
            {
                ModelState.AddModelError("ExpiryDays", "Required.");
            }
            if (!permit.ChargeCollected.HasValue)
            {
                ModelState.AddModelError("ChargeCollected", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            tblPOPermitExpiry pOPermitExpiry = new tblPOPermitExpiry();
                            pOPermitExpiry.PermitId = permit.Id;
                            DateTime latestExpiryDate = dbe.tblPOPermitExpiries
                                .Where(tppe => tppe.PermitId.Equals(permit.Id))
                                .OrderByDescending(tppe => tppe.ExpiryDate).FirstOrDefault().ExpiryDate;
                            pOPermitExpiry.ExpiryDate = latestExpiryDate.AddDays(int.Parse(permit.ExpiryDays));
                            pOPermitExpiry.IsExtension = true;
                            pOPermitExpiry.ChargeCollected = permit.ChargeCollected;
                            dbe.tblPOPermitExpiries.Add(pOPermitExpiry);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-CreatePOPermitExpiry";
                            string object_id = pOPermitExpiry.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            permit = db.tblPermits.Find(permit.Id);
                            TempData["successMsg"] = "Expiry extension successfully created!";
                            return RedirectToAction("PurchaseOrderPermits", new RouteValueDictionary(new { merchantId = permit.MerchantId }));
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            permit = db.tblPermits.Find(permit.Id);
            return View(permit);
        }

        public ActionResult CreateImportPermitExpiry(Guid Id)
        {
            tblPermit permit = db.tblPermits.Find(Id);
            return View(permit);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateImportPermitExpiry(tblPermit permit)
        {
            if (string.IsNullOrEmpty(permit.ExpiryDays))
            {
                ModelState.AddModelError("ExpiryDays", "Required.");
            }
            if (!permit.ChargeCollected.HasValue)
            {
                ModelState.AddModelError("ChargeCollected", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            //Some import permits do not have expiries, so check.
                            tblPOPermitExpiry latestImportPermitExpiry = dbe.tblPOPermitExpiries
                                .Where(tppe => tppe.PermitId.Equals(permit.Id))
                                .OrderByDescending(tppe => tppe.ExpiryDate).FirstOrDefault();
                            tblPOPermitExpiry pOPermitExpiry = null;
                            if (latestImportPermitExpiry != null)
                            {
                                pOPermitExpiry = new tblPOPermitExpiry();
                                pOPermitExpiry.PermitId = permit.Id;
                                pOPermitExpiry.ExpiryDate = latestImportPermitExpiry.ExpiryDate.AddDays(int.Parse(permit.ExpiryDays));
                                pOPermitExpiry.IsExtension = true;
                                pOPermitExpiry.ChargeCollected = permit.ChargeCollected;
                                dbe.tblPOPermitExpiries.Add(pOPermitExpiry);
                            }
                            else
                            {
                                pOPermitExpiry = new tblPOPermitExpiry();
                                DateTime ExpiryDate = dbe.tblPermits.Find(permit.Id).Date.Value;
                                pOPermitExpiry.PermitId = permit.Id;
                                pOPermitExpiry.ExpiryDate = ExpiryDate.AddDays(int.Parse(permit.ExpiryDays));
                                pOPermitExpiry.IsExtension = false;
                                pOPermitExpiry.ChargeCollected = permit.ChargeCollected;
                                dbe.tblPOPermitExpiries.Add(pOPermitExpiry);
                            }
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-CreateImportPermitExpiry";
                            string object_id = pOPermitExpiry.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            permit = db.tblPermits.Find(permit.Id);
                            TempData["successMsg"] = "Expiry extension successfully created!";
                            return RedirectToAction("ImportPermits", new RouteValueDictionary(new { merchantId = permit.MerchantId }));
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            permit = db.tblPermits.Find(permit.Id);
            return View(permit);
        }

        public ActionResult UpdatePurchaseOrderPermitAmount_Auth(int? page)
        {
            numberOfPage = (page ?? 1);
            var permitAmountList = db.tblPermitAmounts
                .Where(tpa => tpa.tbl_lu_Status.name.Equals("Pending")
                && tpa.tblPermit.tblSerialNumberShelf.SerialNumberType.Equals("PO"))
                .OrderByDescending(tpa => tpa.CreatedDate);
            return View(permitAmountList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ActionResult UpdateImportPermitAmount_Auth(int? page)
        {
            numberOfPage = (page ?? 1);
            var permitAmountList = db.tblPermitAmounts
                .Where(tpa => tpa.tbl_lu_Status.name.Equals("Pending")
                && tpa.tblPermit.tblSerialNumberShelf.SerialNumberType.Equals("IMP"))
                .OrderByDescending(tpa => tpa.CreatedDate);
            return View(permitAmountList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ActionResult ApprovePurchaseOrderPermitAmountUpdate(Guid Id)
        {
            tblPermitAmount permitAmount = db.tblPermitAmounts.Find(Id);
            if (permitAmount == null)
            {
                return HttpNotFound();
            }
            return View(permitAmount);
        }

        public ActionResult ApproveImportPermitAmountUpdate(Guid Id)
        {
            tblPermitAmount permitAmount = db.tblPermitAmounts.Find(Id);
            if (permitAmount == null)
            {
                return HttpNotFound();
            }
            return View(permitAmount);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(Guid Id)
        {
            using (var dbe = new ZB_FEPMS_Model())
            {
                using (var dbeTransaction = dbe.Database.BeginTransaction())
                {
                    try
                    {
                        tblPermitAmount permitAmount = dbe.tblPermitAmounts
                            .FirstOrDefault(tpa => tpa.Id.Equals(Id));
                        permitAmount.ApprovalStatusId = dbe.tbl_lu_Status
                            .FirstOrDefault(tls => tls.name.Equals("Approved")).Id;
                        permitAmount.ApprovedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                        permitAmount.ApprovedDate = DateTime.Now;
                        dbe.SaveChanges();
                        tblPermit permit = dbe.tblPermits.Find(permitAmount.PermitId);
                        permit.RemainingAmount += permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount;
                        permit.RemainingAmountInUSD += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD;
                        permit.RemainingAmountInBirr += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr;
                        //Set increased and decreased amounts here
                        if (permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount > 0
                            && permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD > 0
                            && permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr > 0)
                        {
                            if (permit.IncreasedAmount.HasValue && permit.IncreasedAmountInUSD.HasValue && permit.IncreasedAmountInBirr.HasValue)
                            {
                                permit.IncreasedAmount += permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount;
                                permit.IncreasedAmountInUSD += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD;
                                permit.IncreasedAmountInBirr += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr;
                            }
                            else if (!permit.IncreasedAmount.HasValue && !permit.IncreasedAmountInUSD.HasValue && !permit.IncreasedAmountInBirr.HasValue)
                            {
                                permit.IncreasedAmount = permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount;
                                permit.IncreasedAmountInUSD = permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD;
                                permit.IncreasedAmountInBirr = permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr;
                            }
                        }
                        else if (permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount < 0
                            && permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD < 0
                            && permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr < 0)
                        {
                            if (permit.DecreasedAmount.HasValue && permit.DecreasedAmountInUSD.HasValue && permit.DecreasedAmountInBirr.HasValue)
                            {
                                permit.DecreasedAmount += permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount;
                                permit.DecreasedAmountInUSD += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD;
                                permit.DecreasedAmountInBirr += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr;
                            }
                            else if (!permit.DecreasedAmount.HasValue && !permit.DecreasedAmountInUSD.HasValue && !permit.DecreasedAmountInBirr.HasValue)
                            {
                                permit.DecreasedAmount = permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount;
                                permit.DecreasedAmountInUSD = permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD;
                                permit.DecreasedAmountInBirr = permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr;
                            }
                        }
                        dbe.SaveChanges();
                        RBACUser rbacUserObj = new RBACUser();
                        string operation = "Merchant-Approve";
                        string object_id = permitAmount.Id.ToString();
                        rbacUserObj.saveActivityLog(dbe, operation, object_id);
                        dbeTransaction.Commit();
                        TempData["successMsg"] = "Successfully Approved!";
                    }
                    catch (Exception)
                    {
                        dbeTransaction.Rollback();
                        TempData["sErrMsg"] = "Unexpected error occurred.Please try again.";
                    }
                }
            }
            return RedirectToAction("UpdatePurchaseOrderPermitAmount_Auth");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveIPAmountUpdate(Guid Id)
        {
            using (var dbe = new ZB_FEPMS_Model())
            {
                using (var dbeTransaction = dbe.Database.BeginTransaction())
                {
                    try
                    {
                        tblPermitAmount permitAmount = dbe.tblPermitAmounts
                            .FirstOrDefault(tpa => tpa.Id.Equals(Id));
                        permitAmount.ApprovalStatusId = dbe.tbl_lu_Status
                            .FirstOrDefault(tls => tls.name.Equals("Approved")).Id;
                        permitAmount.ApprovedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                        permitAmount.ApprovedDate = DateTime.Now;
                        dbe.SaveChanges();
                        tblPermit permit = dbe.tblPermits.Find(permitAmount.PermitId);
                        List<tblPOPermitAmountDetail> pOPermitAmountDetailList = dbe.tblPOPermitAmountDetails
                            .Where(tpopad => tpopad.PermitAmountId.Equals(permitAmount.Id)).ToList();
                        if (pOPermitAmountDetailList.Count > 0)
                        {
                            decimal Amount = 0, AmountInUSD = 0, AmountInBirr = 0;
                            decimal IncreasedAmount = 0, IncreasedAmountInUSD = 0, IncreasedAmountInBirr = 0;
                            decimal DecreasedAmount = 0, DecreasedAmountInUSD = 0, DecreasedAmountInBirr = 0;
                            foreach (tblPOPermitAmountDetail pOPermitAmountDetail in pOPermitAmountDetailList)
                            {
                                tblPermit _permitObj = pOPermitAmountDetail.tblPermit;
                                _permitObj.RemainingAmount -= pOPermitAmountDetail.Amount;
                                _permitObj.RemainingAmountInUSD -= pOPermitAmountDetail.AmountInUSD;
                                _permitObj.RemainingAmountInBirr -= pOPermitAmountDetail.AmountInBirr;
                                dbe.SaveChanges();
                                Amount += pOPermitAmountDetail.Amount;
                                AmountInUSD += pOPermitAmountDetail.AmountInUSD.Value;
                                AmountInBirr += pOPermitAmountDetail.AmountInBirr.Value;
                                //
                                if (pOPermitAmountDetail.Amount > 0 && pOPermitAmountDetail.AmountInUSD > 0 && pOPermitAmountDetail.AmountInBirr > 0)
                                {
                                    IncreasedAmount += pOPermitAmountDetail.Amount;
                                    IncreasedAmountInUSD += pOPermitAmountDetail.AmountInUSD.Value;
                                    IncreasedAmountInBirr += pOPermitAmountDetail.AmountInBirr.Value;
                                }
                                else if (pOPermitAmountDetail.Amount < 0 && pOPermitAmountDetail.AmountInUSD < 0 && pOPermitAmountDetail.AmountInBirr < 0)
                                {
                                    DecreasedAmount += pOPermitAmountDetail.Amount;
                                    DecreasedAmountInUSD += pOPermitAmountDetail.AmountInUSD.Value;
                                    DecreasedAmountInBirr += pOPermitAmountDetail.AmountInBirr.Value;
                                }
                            }
                            permit.RemainingAmount += Amount;
                            permit.RemainingAmountInUSD += AmountInUSD;
                            permit.RemainingAmountInBirr += AmountInBirr;
                            //Set increased and decreased amounts here
                            if (IncreasedAmount != 0 && IncreasedAmountInUSD != 0 && IncreasedAmountInBirr != 0)
                            {
                                if (permit.IncreasedAmount.HasValue && permit.IncreasedAmountInUSD.HasValue && permit.IncreasedAmountInBirr.HasValue)
                                {
                                    permit.IncreasedAmount += IncreasedAmount;
                                    permit.IncreasedAmountInUSD += IncreasedAmountInUSD;
                                    permit.IncreasedAmountInBirr += IncreasedAmountInBirr;
                                }
                                else if (!permit.IncreasedAmount.HasValue && !permit.IncreasedAmountInUSD.HasValue && !permit.IncreasedAmountInBirr.HasValue)
                                {
                                    permit.IncreasedAmount = IncreasedAmount;
                                    permit.IncreasedAmountInUSD = IncreasedAmountInUSD;
                                    permit.IncreasedAmountInBirr = IncreasedAmountInBirr;
                                }
                            }
                            if (DecreasedAmount != 0 && DecreasedAmountInUSD != 0 && DecreasedAmountInBirr != 0)
                            {
                                if (permit.DecreasedAmount.HasValue && permit.DecreasedAmountInUSD.HasValue && permit.DecreasedAmountInBirr.HasValue)
                                {
                                    permit.DecreasedAmount += DecreasedAmount;
                                    permit.DecreasedAmountInUSD += DecreasedAmountInUSD;
                                    permit.DecreasedAmountInBirr += DecreasedAmountInBirr;
                                }
                                else if (!permit.DecreasedAmount.HasValue && !permit.DecreasedAmountInUSD.HasValue && !permit.DecreasedAmountInBirr.HasValue)
                                {
                                    permit.DecreasedAmount = DecreasedAmount;
                                    permit.DecreasedAmountInUSD = DecreasedAmountInUSD;
                                    permit.DecreasedAmountInBirr = DecreasedAmountInBirr;
                                }
                            }
                        }
                        else
                        {
                            permit.RemainingAmount += permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount;
                            permit.RemainingAmountInUSD += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD;
                            permit.RemainingAmountInBirr += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr;
                            //Set increased and decreased amounts here
                            if (permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount > 0
                                && permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD > 0
                                && permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr > 0)
                            {
                                if (permit.IncreasedAmount.HasValue && permit.IncreasedAmountInUSD.HasValue && permit.IncreasedAmountInBirr.HasValue)
                                {
                                    permit.IncreasedAmount += permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount;
                                    permit.IncreasedAmountInUSD += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD;
                                    permit.IncreasedAmountInBirr += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr;
                                }
                                else if (!permit.IncreasedAmount.HasValue && !permit.IncreasedAmountInUSD.HasValue && !permit.IncreasedAmountInBirr.HasValue)
                                {
                                    permit.IncreasedAmount = permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount;
                                    permit.IncreasedAmountInUSD = permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD;
                                    permit.IncreasedAmountInBirr = permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr;
                                }
                            }
                            else if (permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount < 0
                                && permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD < 0
                                && permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr < 0)
                            {
                                if (permit.DecreasedAmount.HasValue && permit.DecreasedAmountInUSD.HasValue && permit.DecreasedAmountInBirr.HasValue)
                                {
                                    permit.DecreasedAmount += permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount;
                                    permit.DecreasedAmountInUSD += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD;
                                    permit.DecreasedAmountInBirr += permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr;
                                }
                                else if (!permit.DecreasedAmount.HasValue && !permit.DecreasedAmountInUSD.HasValue && !permit.DecreasedAmountInBirr.HasValue)
                                {
                                    permit.DecreasedAmount = permitAmount.tblPermitAmountDetails.FirstOrDefault().Amount;
                                    permit.DecreasedAmountInUSD = permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInUSD;
                                    permit.DecreasedAmountInBirr = permitAmount.tblPermitAmountDetails.FirstOrDefault().AmountInBirr;
                                }
                            }
                            dbe.SaveChanges();
                        }
                        RBACUser rbacUserObj = new RBACUser();
                        string operation = "Merchant-ApproveIPAmountUpdate";
                        string object_id = permitAmount.Id.ToString();
                        rbacUserObj.saveActivityLog(dbe, operation, object_id);
                        dbeTransaction.Commit();
                        TempData["successMsg"] = "Successfully Approved!";
                    }
                    catch (Exception)
                    {
                        dbeTransaction.Rollback();
                        TempData["sErrMsg"] = "Unexpected error occurred.Please try again.";
                    }
                }
            }
            return RedirectToAction("UpdateImportPermitAmount_Auth");
        }

        public ActionResult Reject(Guid Id)
        {
            tblPermitAmount permitAmount = db.tblPermitAmounts.Find(Id);
            if (permitAmount == null)
            {
                return HttpNotFound();
            }
            return View(permitAmount);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(Guid Id, string Remark)
        {
            if (string.IsNullOrEmpty(Remark))
            {
                ModelState.AddModelError("Remark", "Required");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            tblPermitAmount permitAmount = dbe.tblPermitAmounts
                            .FirstOrDefault(tpa => tpa.Id.Equals(Id));
                            permitAmount.ApprovalStatusId = dbe.tbl_lu_Status
                                .FirstOrDefault(tls => tls.name.Equals("Rejected")).Id;
                            permitAmount.ApprovedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                            permitAmount.ApprovedDate = DateTime.Now;
                            permitAmount.Remark = Remark;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-Reject";
                            string object_id = permitAmount.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Successfully rejected!";
                            return RedirectToAction("UpdatePurchaseOrderPermitAmount_Auth");
                        }
                        catch (Exception)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            TempData["sErrMsg"] = "Remark is required.";
            return RedirectToAction("Reject", new RouteValueDictionary(new { Id = Id }));
        }

        public ActionResult RejectIPAmountUpdate(Guid Id)
        {
            tblPermitAmount permitAmount = db.tblPermitAmounts.Find(Id);
            if (permitAmount == null)
            {
                return HttpNotFound();
            }
            return View(permitAmount);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectIPAmountUpdate(Guid Id, string Remark)
        {
            if (string.IsNullOrEmpty(Remark))
            {
                ModelState.AddModelError("Remark", "Required");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            tblPermitAmount permitAmount = dbe.tblPermitAmounts
                            .FirstOrDefault(tpa => tpa.Id.Equals(Id));
                            permitAmount.ApprovalStatusId = dbe.tbl_lu_Status
                                .FirstOrDefault(tls => tls.name.Equals("Rejected")).Id;
                            permitAmount.ApprovedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                            permitAmount.ApprovedDate = DateTime.Now;
                            permitAmount.Remark = Remark;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-RejectIPAmountUpdate";
                            string object_id = permitAmount.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Successfully rejected!";
                            return RedirectToAction("UpdateImportPermitAmount_Auth");
                        }
                        catch (Exception)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            TempData["sErrMsg"] = "Remark is required.";
            return RedirectToAction("RejectIPAmountUpdate", new RouteValueDictionary(new { Id = Id }));
        }

        public tblPermit initMerchantFormImport(tblPermit permit)
        {
            List<string> currencyTypeList = new List<string>();
            //string date = DateTime.Now.DayOfWeek.ToString();
            //if (date == "Saturday")
            //{
            //    currencyTypeList = selectListOfCurrencyTypesSAT();
            //}
            //else if (date == "Sunday")
            //{
            //    currencyTypeList = selectListOfCurrencyTypesSUN();
            //}
            //else
            //{
            currencyTypeList = selectListOfCurrencyTypes();
            //}
            List<SelectListItem> currencyTypes = new List<SelectListItem>();
            foreach (string currencyType in currencyTypeList)
            {
                SelectListItem selectListItem = new SelectListItem();
                selectListItem.Text = currencyType;
                selectListItem.Value = currencyType;
                currencyTypes.Add(selectListItem);
            }
            ViewBag.CurrencyType = currencyTypes;
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")), "Id", "name", db.tbl_lu_Status
                .FirstOrDefault(tls => tls.name.Equals("Active")).Id);
            List<SelectListItem> approvalStatus = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NBE", Value = "NBE"
                },
                new SelectListItem {
                    Text = "Queue", Value = "Queue"
                },
                new SelectListItem {
                    Text = "Own Source", Value = "Own Source"
                },
                new SelectListItem {
                    Text = "President", Value = "President"
                },
                new SelectListItem {
                    Text = "On Demand", Value = "On Demand"
                }
            };
            ViewBag.ApprovalStatus = approvalStatus;
            List<SelectListItem> ownSourceValue = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NRFCY", Value = "NRFCY"
                },
                new SelectListItem {
                    Text = "Retention", Value = "Retention"
                },
                new SelectListItem {
                    Text = "Diaspora", Value = "Diaspora"
                },
            };
            ViewBag.OwnSourceValue = ownSourceValue;
            ViewBag.SelectedCountryOfOriginIds = new MultiSelectList(db.tbl_lu_CountryOfOrigin
                .OrderBy(tlc => tlc.name), "Id", "name");
            //$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$
            ViewBag.SelectedPurchaseOrderIds = new MultiSelectList(permit.ImportPOList.OrderByDescending(ipl => ipl.Date), "Id", "PermitNumberCurrencyType");
            //$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$
            ViewBag.MethodOfPaymentId = new SelectList(db.tbl_lu_MethodOfPayment
                .OrderBy(tlc => tlc.name), "Id", "name");
            ViewBag.SelectedPortOfLoadingIds = new MultiSelectList(db.tbl_lu_PortOfLoading
                            .OrderBy(tlpol => tlpol.name), "Id", "name");
            ViewBag.SelectedPortOfDestinationIds = new MultiSelectList(db.tbl_lu_PortOfDestination
                            .OrderBy(tlpod => tlpod.name), "Id", "name");
            ViewBag.SelectedShipmentAllowedByIds = new MultiSelectList(db.tbl_lu_ShipmentAllowedBy
                            .OrderBy(tlsab => tlsab.name), "Id", "name");
            ViewBag.SelectedIncotermIds = new MultiSelectList(db.tbl_lu_Incoterm
                            .OrderBy(tli => tli.name), "Id", "name");
            List<tblItemPriority> priorityList = db.tblItemPriorities.OrderBy(tip => tip.GroupBy).ToList();
            permit.firstPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("First Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            permit.secondPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Second Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            permit.thirdPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Third Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            permit.firstPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("First Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            permit.secondPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Second Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            permit.thirdPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Third Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            return permit;
        }
        public tblPermit initMerchantFormErrorImport(tblPermit permit)
        {
            List<string> currencyTypeList = new List<string>();
            //string date = DateTime.Now.DayOfWeek.ToString();
            //if (date == "Saturday")
            //{
            //    currencyTypeList = selectListOfCurrencyTypesSAT();
            //}
            //else if (date == "Sunday")
            //{
            //    currencyTypeList = selectListOfCurrencyTypesSUN();
            //}
            //else
            //{
            currencyTypeList = selectListOfCurrencyTypes();
            //}
            List<SelectListItem> currencyTypes = new List<SelectListItem>();
            foreach (string currencyType in currencyTypeList)
            {
                SelectListItem selectListItem = new SelectListItem();
                selectListItem.Text = currencyType;
                selectListItem.Value = currencyType;
                currencyTypes.Add(selectListItem);
            }
            ViewBag.CurrencyType = currencyTypes;
            //##
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")), "Id", "name", db.tbl_lu_Status
                .FirstOrDefault(tls => tls.name.Equals("Active")).Id);
            List<SelectListItem> approvalStatus = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NBE", Value = "NBE"
                },
                new SelectListItem {
                    Text = "Queue", Value = "Queue"
                },
                new SelectListItem {
                    Text = "Own Source", Value = "Own Source"
                },
                new SelectListItem {
                    Text = "President", Value = "President"
                },
                new SelectListItem {
                    Text = "On Demand", Value = "On Demand"
                }
            };
            ViewBag.ApprovalStatus = approvalStatus;
            List<SelectListItem> ownSourceValue = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NRFCY", Value = "NRFCY"
                },
                new SelectListItem {
                    Text = "Retention", Value = "Retention"
                },
                new SelectListItem {
                    Text = "Diaspora", Value = "Diaspora"
                },
            };
            ViewBag.OwnSourceValue = ownSourceValue;
            ViewBag.SelectedCountryOfOriginIds = new MultiSelectList(db.tbl_lu_CountryOfOrigin
                .OrderBy(tlc => tlc.name), "Id", "name");
            //$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$
            ViewBag.SelectedPurchaseOrderIds = new MultiSelectList(permit.ImportPOList.OrderByDescending(ipl => ipl.Date), "Id", "PermitNumber");
            //$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$
            ViewBag.MethodOfPaymentId = new SelectList(db.tbl_lu_MethodOfPayment
                .OrderBy(tlc => tlc.name), "Id", "name");
            ViewBag.SelectedPortOfLoadingIds = new MultiSelectList(db.tbl_lu_PortOfLoading
                            .OrderBy(tlpol => tlpol.name), "Id", "name");
            ViewBag.SelectedPortOfDestinationIds = new MultiSelectList(db.tbl_lu_PortOfDestination
                            .OrderBy(tlpod => tlpod.name), "Id", "name");
            ViewBag.SelectedShipmentAllowedByIds = new MultiSelectList(db.tbl_lu_ShipmentAllowedBy
                            .OrderBy(tlsab => tlsab.name), "Id", "name");
            ViewBag.SelectedIncotermIds = new MultiSelectList(db.tbl_lu_Incoterm
                            .OrderBy(tli => tli.name), "Id", "name");
            //####################################################################
            List<tblItemPriority> priorityList = db.tblItemPriorities.OrderBy(tip => tip.GroupBy).ToList();
            permit.firstPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("First Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.selectedFirstPriorityTopLevels.Contains(c.FirstOrDefault().GroupBy)
                }).ToList();
            permit.secondPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Second Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.selectedSecondPriorityTopLevels.Contains(c.FirstOrDefault().GroupBy)
                }).ToList();
            permit.thirdPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Third Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.selectedThirdPriorityTopLevels.Contains(c.FirstOrDefault().GroupBy)
                }).ToList();
            permit.firstPrioritySubLevels = priorityList
               .Where(pl => pl.Priority.Equals("First Priority")
               && permit.selectedFirstPriorityTopLevels.Contains(pl.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.selectedFirstPrioritySubLevels.Contains(c.GroupBy + "-" + c.Name)
               }).ToList();
            permit.secondPrioritySubLevels = priorityList
               .Where(pl => pl.Priority.Equals("Second Priority")
               && permit.selectedSecondPriorityTopLevels.Contains(pl.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.selectedSecondPrioritySubLevels.Contains(c.GroupBy + "-" + c.Name)
               }).ToList();
            permit.thirdPrioritySubLevels = priorityList
               .Where(pl => pl.Priority.Equals("Third Priority")
               && permit.selectedThirdPriorityTopLevels.Contains(pl.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.selectedThirdPrioritySubLevels.Contains(c.GroupBy + "-" + c.Name)
               }).ToList();
            return permit;
        }

        public ActionResult CreateImportPermit(Guid Id)
        {
            tblPermit permit = new tblPermit();
            tblMerchant merchant = db.tblMerchants.Find(Id);
            permit.ImporterName = merchant.ImporterName;
            permit.TinNumber = merchant.TinNumber;
            permit.NBENumber = merchant.NBENumber;
            permit.MerchantId = merchant.Id;
            permit.ImportPOList = merchant.tblPermits
                .Where(tp => tp.tblSerialNumberShelf.SerialNumberType.Equals("PO")
                && tp.RemainingAmount > 0)
                .Select(c => new tblPermit()
                {
                    Id = c.Id,
                    PermitNumber = c.PermitNumber,
                    Status = c.tbl_lu_Status.name,
                    CurrencyType = c.CurrencyType,
                    RemainingAmount = c.RemainingAmount,
                    ExpiredYesNo = c.tblPOPermitExpiries.Any(tppe => tppe.ExpiryDate >= DateTime.Now.Date)
                    ? "No" : "Yes",
                    Date = c.Date
                }).OrderByDescending(tp => tp.Date).ToList();
            return View(initMerchantFormImport(permit));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateImportPermit(tblPermit permit)
        {
            tbl_lu_MethodOfPayment methodOfPayment = null;
            if (string.IsNullOrEmpty(permit.LPCONumber))
            {
                ModelState.AddModelError("LPCONumber", "Required.");
            }
            if (string.IsNullOrEmpty(permit.ExpiryDays))
            {
                ModelState.AddModelError("ExpiryDays", "Required.");
            }
            if (permit.MethodOfPaymentId == null)
            {
                ModelState.AddModelError("MethodOfPaymentId", "Required.");
            }
            else
            {
                methodOfPayment = db.tbl_lu_MethodOfPayment.Find(permit.MethodOfPaymentId);
                if (string.IsNullOrEmpty(permit.CurrencyType))
                {
                    ModelState.AddModelError("CurrencyType", "Required.");
                }
                if (string.IsNullOrEmpty(permit.CurrencyRateValue)
                    || permit.CurrencyRateValue.Equals("0"))
                {
                    ModelState.AddModelError("CurrencyRateValue", "Required.");
                }
                if (string.IsNullOrEmpty(permit.USDRateValue)
                    || permit.USDRateValue.Equals("0"))
                {
                    ModelState.AddModelError("USDRateValue", "Required.");
                }
                if (!string.IsNullOrEmpty(permit.TypeOfImportPermit) && permit.TypeOfImportPermit.Equals("NonPO"))
                {
                    if (string.IsNullOrEmpty(permit.AmountValue)
                    || permit.AmountValue.Equals("0"))
                    {
                        ModelState.AddModelError("AmountValue", "Required.");
                    }
                    if (string.IsNullOrEmpty(permit.AmountInUSDValue)
                        || permit.AmountInUSDValue.Equals("0"))
                    {
                        ModelState.AddModelError("AmountInUSDValue", "Required.");
                    }
                    if (string.IsNullOrEmpty(permit.AmountInBirrValue)
                        || permit.AmountInBirrValue.Equals("0"))
                    {
                        ModelState.AddModelError("AmountInBirrValue", "Required.");
                    }
                }
            }
            if (string.IsNullOrEmpty(permit.ApprovalStatus))
            {
                ModelState.AddModelError("ApprovalStatus", "Required.");
            }
            else
            {
                if (permit.ApprovalStatus.Equals("NBE"))
                {
                    if (string.IsNullOrEmpty(permit.NBEApprovalRefNumber))
                    {
                        ModelState.AddModelError("NBEApprovalRefNumber", "Required.");
                    }
                }
                else if (permit.ApprovalStatus.Equals("Queue"))
                {
                    if (string.IsNullOrEmpty(permit.QueueRound))
                    {
                        ModelState.AddModelError("QueueRound", "Required.");
                    }
                    if (string.IsNullOrEmpty(permit.QueueNumber))
                    {
                        ModelState.AddModelError("QueueNumber", "Required.");
                    }
                }
                else if (permit.ApprovalStatus.Equals("Own Source"))
                {
                    if (string.IsNullOrEmpty(permit.OwnSourceValue))
                    {
                        ModelState.AddModelError("OwnSourceValue", "Required.");
                    }
                }
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {

                        try
                        {
                            List<Guid> permitIds = new List<Guid>();
                            List<tblPermit> permitList = null;
                            List<tblPOPermitDetail> tblPOPermitDetails = new List<tblPOPermitDetail>();
                            bool errorHappened = false;
                            if (!string.IsNullOrEmpty(permit.TypeOfImportPermit) && permit.TypeOfImportPermit.Equals("PO"))
                            {
                                foreach (tblPermit permitObj in permit.ImportPOList)
                                {
                                    permitIds.Add(permitObj.Id);
                                }
                                permitList = dbe.tblPermits.Where(tp => permitIds.Contains(tp.Id)).ToList();
                                int counter = 0;
                                foreach (tblPermit permitObj in permit.ImportPOList)
                                {
                                    if (!string.IsNullOrEmpty(permitObj.AmountValue))
                                    {
                                        tblPermit _permitObj = permitList.FirstOrDefault(pl => pl.Id.Equals(permitObj.Id));
                                        if ((_permitObj.RemainingAmount - formatDecimal(permitObj.AmountValue)) < 0)
                                        {
                                            ModelState.AddModelError("ImportPOList[" + counter + "].AmountValue", "Amount is greater than remaining amount.");
                                            errorHappened = true;
                                        }
                                        if (!_permitObj.CurrencyType.Equals(permit.CurrencyType))
                                        {
                                            ModelState.AddModelError("ImportPOList[" + counter + "].AmountValue", "Currency type is wrong.");
                                            errorHappened = true;
                                        }
                                        if (!_permitObj.tblPOPermitExpiries.Any(tppe => tppe.ExpiryDate >= DateTime.Now.Date))
                                        {
                                            ModelState.AddModelError("ImportPOList[" + counter + "].AmountValue", "PO is expired.");
                                            errorHappened = true;
                                        }
                                        if (!permitObj.Status.Equals("Active"))
                                        {
                                            ModelState.AddModelError("ImportPOList[" + counter + "].AmountValue", "PO is " + permitObj.Status);
                                            errorHappened = true;
                                        }
                                    }
                                    counter++;
                                }
                                bool poAmountSet = false;
                                foreach (tblPermit permitObj in permit.ImportPOList)
                                {
                                    if (!string.IsNullOrEmpty(permitObj.AmountValue)
                                        && !permitObj.AmountValue.Equals("0")
                                        && !string.IsNullOrEmpty(permitObj.AmountInUSDValue)
                                        && !permitObj.AmountInUSDValue.Equals("0")
                                        && !string.IsNullOrEmpty(permitObj.AmountInBirrValue)
                                        && !permitObj.AmountInBirrValue.Equals("0"))
                                    {
                                        poAmountSet = true;
                                        break;
                                    }
                                }
                                if (!poAmountSet)
                                {
                                    ModelState.AddModelError("MethodOfPaymentId", "Please set amount in below table.");
                                    errorHappened = true;
                                }
                            }
                            if (errorHappened)
                            {
                                return View(initMerchantFormErrorImport(permit));
                            }
                            string currentYear = selectCurrentYear();
                            tblPermit _Permit = new tblPermit();
                            _Permit.CreatedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                            _Permit.MerchantId = permit.MerchantId;
                            int nextSerialNumberValue = returnNextSerialNumberValueByType(dbe, "IMP", currentYear);
                            tblSerialNumberShelf prevSerial = dbe.tblSerialNumberShelves
                                .FirstOrDefault(tsns => tsns.SerialNumberType.Equals("IMP")
                                && tsns.IsLatest == true);
                            prevSerial.IsLatest = false;
                            dbe.SaveChanges();
                            tblSerialNumberShelf serialNumberShelf = new tblSerialNumberShelf();
                            serialNumberShelf.SerialNumberType = "IMP";
                            serialNumberShelf.SerialNumberValue = nextSerialNumberValue;
                            serialNumberShelf.IsLatest = true;
                            serialNumberShelf.Year = currentYear;
                            dbe.tblSerialNumberShelves.Add(serialNumberShelf);
                            dbe.SaveChanges();
                            _Permit.MethodOfPaymentId = permit.MethodOfPaymentId;
                            _Permit.LPCONumber = permit.LPCONumber;
                            _Permit.SerialNumberShelfId = serialNumberShelf.Id;
                            _Permit.PermitType = "01";
                            _Permit.PermitYear = currentYear;
                            _Permit.Date = DateTime.Now;
                            _Permit.CreatedDate = DateTime.Now;
                            _Permit.PermitStatusId = permit.PermitStatusId;
                            _Permit.PermitNumber = "ZEB-ZBH-01-"
                                + serialNumberShelf.SerialNumberValue.ToString().PadLeft(5, '0')
                                + "-" + _Permit.PermitYear;
                            _Permit.CurrencyType = permit.CurrencyType;
                            _Permit.CurrencyRate = formatDecimal(permit.CurrencyRateValue);
                            _Permit.USDRate = formatDecimal(permit.USDRateValue);
                            if (!string.IsNullOrEmpty(permit.TypeOfImportPermit) && permit.TypeOfImportPermit.Equals("PO"))
                            {
                                decimal Amount = 0, AmountInUSD = 0, AmountInBirr = 0;
                                foreach (tblPermit permitObj in permit.ImportPOList)
                                {
                                    if (!string.IsNullOrEmpty(permitObj.AmountValue)
                                        && !permitObj.AmountValue.Equals("0")
                                        && !string.IsNullOrEmpty(permitObj.AmountInUSDValue)
                                        && !permitObj.AmountInUSDValue.Equals("0")
                                        && !string.IsNullOrEmpty(permitObj.AmountInBirrValue)
                                        && !permitObj.AmountInBirrValue.Equals("0"))
                                    {
                                        tblPermit _permitObj = permitList.FirstOrDefault(pl => pl.Id.Equals(permitObj.Id));
                                        if (_permitObj.tblSerialNumberShelf.SerialNumberType.Equals("PO")
                                             && _permitObj.RemainingAmount > 0
                                             && ((_permitObj.RemainingAmount - formatDecimal(permitObj.AmountValue)) >= 0)
                                             && _permitObj.CurrencyType.Equals(permit.CurrencyType)
                                             && _permitObj.tbl_lu_Status.name.Equals("Active")
                                             && _permitObj.tblPOPermitExpiries
                                             .Any(tppe => tppe.ExpiryDate >= DateTime.Now.Date))
                                        {
                                            tblPOPermitDetail tblPOPermitDetail = new tblPOPermitDetail();
                                            tblPOPermitDetail.POId = _permitObj.Id;
                                            tblPOPermitDetail.PermitId = _Permit.Id;
                                            tblPOPermitDetail.AmountBeforePermit = _permitObj.RemainingAmount;
                                            tblPOPermitDetail.AmountAfterPermit = _permitObj.RemainingAmount - formatDecimal(permitObj.AmountValue);
                                            tblPOPermitDetail.AmountInUSDBeforePermit = _permitObj.RemainingAmountInUSD;
                                            tblPOPermitDetail.AmountInUSDAfterPermit = _permitObj.RemainingAmountInUSD - formatDecimal(permitObj.AmountInUSDValue);
                                            tblPOPermitDetail.AmountInBirrBeforePermit = _permitObj.RemainingAmountInBirr;
                                            tblPOPermitDetail.AmountInBirrAfterPermit = _permitObj.RemainingAmountInBirr - formatDecimal(permitObj.AmountInBirrValue);
                                            tblPOPermitDetail.CreatedDate = DateTime.Now;
                                            tblPOPermitDetails.Add(tblPOPermitDetail);
                                            _permitObj.RemainingAmount -= formatDecimal(permitObj.AmountValue);
                                            _permitObj.RemainingAmountInUSD -= formatDecimal(permitObj.AmountInUSDValue);
                                            _permitObj.RemainingAmountInBirr -= formatDecimal(permitObj.AmountInBirrValue);
                                            Amount += formatDecimal(permitObj.AmountValue);
                                            AmountInUSD += formatDecimal(permitObj.AmountInUSDValue);
                                            AmountInBirr += formatDecimal(permitObj.AmountInBirrValue);
                                            dbe.SaveChanges();
                                        }
                                    }
                                }
                                _Permit.Amount = Amount;
                                _Permit.RemainingAmount = Amount;
                                _Permit.AmountInUSD = AmountInUSD;
                                _Permit.RemainingAmountInUSD = AmountInUSD;
                                _Permit.AmountInBirr = AmountInBirr;
                                _Permit.RemainingAmountInBirr = AmountInBirr;
                            }
                            else if (!string.IsNullOrEmpty(permit.TypeOfImportPermit) && permit.TypeOfImportPermit.Equals("NonPO"))
                            {
                                _Permit.Amount = formatDecimal(permit.AmountValue);
                                _Permit.RemainingAmount = _Permit.Amount;
                                _Permit.AmountInUSD = formatDecimal(permit.AmountInUSDValue);
                                _Permit.RemainingAmountInUSD = _Permit.AmountInUSD;
                                _Permit.AmountInBirr = formatDecimal(permit.AmountInBirrValue);
                                _Permit.RemainingAmountInBirr = _Permit.AmountInBirr;
                            }
                            _Permit.NonPriorityItems = permit.NonPriorityItems;
                            if (permit.ApprovalStatus.Equals("NBE"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.NBEApprovalRefNumber = permit.NBEApprovalRefNumber;
                            }
                            else if (permit.ApprovalStatus.Equals("Queue"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.QueueRound = permit.QueueRound;
                                _Permit.QueueNumber = permit.QueueNumber;
                            }
                            else if (permit.ApprovalStatus.Equals("Own Source"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.OwnSourceValue = permit.OwnSourceValue;
                            }
                            else if (permit.ApprovalStatus.Equals("President")
                                || permit.ApprovalStatus.Equals("On Demand"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                            }
                            dbe.tblPermits.Add(_Permit);
                            List<tbl_lu_PortOfLoading> portOfLoadings = dbe.tbl_lu_PortOfLoading.ToList();
                            List<tbl_lu_PortOfDestination> portOfDestinations = dbe.tbl_lu_PortOfDestination.ToList();
                            List<tbl_lu_ShipmentAllowedBy> shipmentAllowedBies = dbe.tbl_lu_ShipmentAllowedBy.ToList();
                            List<tbl_lu_Incoterm> incoterms = dbe.tbl_lu_Incoterm.ToList();
                            List<tbl_lu_CountryOfOrigin> countryOfOrigins = dbe.tbl_lu_CountryOfOrigin.ToList();
                            if (permit.SelectedPortOfLoadingIds != null)
                            {
                                foreach (Guid Id in permit.SelectedPortOfLoadingIds)
                                {
                                    _Permit.tbl_lu_PortOfLoading.Add(portOfLoadings.FirstOrDefault(pol => pol.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedPortOfDestinationIds != null)
                            {
                                foreach (Guid Id in permit.SelectedPortOfDestinationIds)
                                {
                                    _Permit.tbl_lu_PortOfDestination.Add(portOfDestinations.FirstOrDefault(pod => pod.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedShipmentAllowedByIds != null)
                            {
                                foreach (Guid Id in permit.SelectedShipmentAllowedByIds)
                                {
                                    _Permit.tbl_lu_ShipmentAllowedBy.Add(shipmentAllowedBies.FirstOrDefault(sab => sab.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedIncotermIds != null)
                            {
                                foreach (Guid Id in permit.SelectedIncotermIds)
                                {
                                    _Permit.tbl_lu_Incoterm.Add(incoterms.FirstOrDefault(i => i.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedCountryOfOriginIds != null)
                            {
                                foreach (Guid Id in permit.SelectedCountryOfOriginIds)
                                {
                                    _Permit.tbl_lu_CountryOfOrigin.Add(countryOfOrigins.FirstOrDefault(coo => coo.Id.Equals(Id)));
                                }
                            }
                            List<tblItemPriority> itemPriorities = dbe.tblItemPriorities.ToList();
                            if (permit.selectedFirstPrioritySubLevels != null)
                            {
                                foreach (string selectedFirstPriorityItem in permit.selectedFirstPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("First Priority")
                                            && groupByName.Equals(selectedFirstPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            if (permit.selectedSecondPrioritySubLevels != null)
                            {
                                foreach (string selectedSecondPriorityItem in permit.selectedSecondPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("Second Priority")
                                            && groupByName.Equals(selectedSecondPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            if (permit.selectedThirdPrioritySubLevels != null)
                            {
                                foreach (string selectedThirdPriorityItem in permit.selectedThirdPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("Third Priority")
                                            && groupByName.Equals(selectedThirdPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            tblPOPermitExpiry pOPermitExpiry = new tblPOPermitExpiry();
                            pOPermitExpiry.PermitId = _Permit.Id;
                            pOPermitExpiry.ExpiryDate = _Permit.Date.Value.AddDays(int.Parse(permit.ExpiryDays));
                            pOPermitExpiry.IsExtension = false;
                            dbe.tblPOPermitExpiries.Add(pOPermitExpiry);
                            dbe.tblPOPermitDetails.AddRange(tblPOPermitDetails);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-CreateImportPermit";
                            string object_id = _Permit.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Import permit successfully created!";
                            return RedirectToAction("ImportPermitConfirmation", new RouteValueDictionary(new { Id = _Permit.Id }));
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            return View(initMerchantFormErrorImport(permit));
        }
        public tblPermit initEditImportPermitForm(tblPermit permit)
        {
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")), "Id", "name", permit.PermitStatusId);
            ViewBag.MethodOfPaymentId = new SelectList(db.tbl_lu_MethodOfPayment
                .OrderBy(tlmp => tlmp.name), "Id", "name", permit.MethodOfPaymentId);
            ViewBag.SelectedPortOfLoadingIds = new MultiSelectList(db.tbl_lu_PortOfLoading
                            .OrderBy(tlpol => tlpol.name), "Id", "name", permit.tbl_lu_PortOfLoading.Select(tlpol => tlpol.Id));
            ViewBag.SelectedPortOfDestinationIds = new MultiSelectList(db.tbl_lu_PortOfDestination
                            .OrderBy(tlpod => tlpod.name), "Id", "name", permit.tbl_lu_PortOfDestination.Select(tlpod => tlpod.Id));
            ViewBag.SelectedShipmentAllowedByIds = new MultiSelectList(db.tbl_lu_ShipmentAllowedBy
                            .OrderBy(tlsab => tlsab.name), "Id", "name", permit.tbl_lu_ShipmentAllowedBy.Select(tlsab => tlsab.Id));
            ViewBag.SelectedIncotermIds = new MultiSelectList(db.tbl_lu_Incoterm
                            .OrderBy(tli => tli.name), "Id", "name", permit.tbl_lu_Incoterm.Select(tli => tli.Id));
            ViewBag.SelectedCountryOfOriginIds = new MultiSelectList(db.tbl_lu_CountryOfOrigin
                .OrderBy(tlc => tlc.name), "Id", "name", permit.tbl_lu_CountryOfOrigin.Select(tlcoo => tlcoo.Id));
            List<SelectListItem> approvalStatus = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NBE", Value = "NBE", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("NBE")
                },
                new SelectListItem {
                    Text = "Queue", Value = "Queue", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("Queue")
                },
                new SelectListItem {
                    Text = "Own Source", Value = "Own Source", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("Own Source")
                },
                new SelectListItem {
                    Text = "President", Value = "President", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("President")
                },
                new SelectListItem {
                    Text = "On Demand", Value = "On Demand", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("On Demand")
                }
            };
            ViewBag.ApprovalStatus = approvalStatus;
            List<SelectListItem> ownSourceValue = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NRFCY", Value = "NRFCY", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("NRFCY")
                },
                new SelectListItem {
                    Text = "Retention", Value = "Retention", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("Retention")
                },
                new SelectListItem {
                    Text = "Diaspora", Value = "Diaspora", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("Diaspora")
                },
            };
            ViewBag.OwnSourceValue = ownSourceValue;
            List<tblItemPriority> priorityList = db.tblItemPriorities.OrderBy(tip => tip.GroupBy).ToList();
            permit.firstPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("First Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.firstPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedFirstPriorityItem in permit.firstPriorityTopLevels)
                {
                    if (selectedFirstPriorityItem.Selected)
                    {
                        permit.selectedFirstPriorityTopLevels.Add(selectedFirstPriorityItem.Value);
                    }
                }
            }
            permit.secondPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Second Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.secondPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedSecondPriorityItem in permit.secondPriorityTopLevels)
                {
                    if (selectedSecondPriorityItem.Selected)
                    {
                        permit.selectedSecondPriorityTopLevels.Add(selectedSecondPriorityItem.Value);
                    }
                }
            }
            permit.thirdPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Third Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.thirdPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedThirdPriorityItem in permit.thirdPriorityTopLevels)
                {
                    if (selectedThirdPriorityItem.Selected)
                    {
                        permit.selectedThirdPriorityTopLevels.Add(selectedThirdPriorityItem.Value);
                    }
                }
            }
            permit.firstPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("First Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.firstPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedFirstPriorityItem in permit.firstPrioritySubLevels)
                {
                    if (selectedFirstPriorityItem.Selected)
                    {
                        permit.selectedFirstPrioritySubLevels.Add(selectedFirstPriorityItem.Value);
                    }
                }
            }
            permit.secondPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Second Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.secondPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedSecondPriorityItem in permit.secondPrioritySubLevels)
                {
                    if (selectedSecondPriorityItem.Selected)
                    {
                        permit.selectedSecondPrioritySubLevels.Add(selectedSecondPriorityItem.Value);
                    }
                }
            }
            permit.thirdPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Third Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.thirdPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedThirdPriorityItem in permit.thirdPrioritySubLevels)
                {
                    if (selectedThirdPriorityItem.Selected)
                    {
                        permit.selectedThirdPrioritySubLevels.Add(selectedThirdPriorityItem.Value);
                    }
                }
            }
            //Some import permits do not have expiries, so check.
            bool expiryExists = permit.tblPOPermitExpiries.Any();
            if (expiryExists)
            {
                DateTime expiryDate = permit.tblPOPermitExpiries.FirstOrDefault(tppe => tppe.IsExtension == false).ExpiryDate;
                DateTime permitDate = permit.Date.Value.Date;
                permit.ExpiryDays = (expiryDate - permitDate).Days.ToString();
            }
            return permit;
        }
        public tblPermit initEditImportPermitFormError(tblPermit permit)
        {
            permit = db.tblPermits.Find(permit.Id);
            permit.ImporterName = permit.tblMerchant.ImporterName;
            permit.TinNumber = permit.tblMerchant.TinNumber;
            permit.NBENumber = permit.tblMerchant.NBENumber;
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")), "Id", "name", permit.PermitStatusId);
            ViewBag.MethodOfPaymentId = new SelectList(db.tbl_lu_MethodOfPayment
                .OrderBy(tlmp => tlmp.name), "Id", "name", permit.MethodOfPaymentId);
            ViewBag.SelectedPortOfLoadingIds = new MultiSelectList(db.tbl_lu_PortOfLoading
                            .OrderBy(tlpol => tlpol.name), "Id", "name", permit.tbl_lu_PortOfLoading.Select(tlpol => tlpol.Id));
            ViewBag.SelectedPortOfDestinationIds = new MultiSelectList(db.tbl_lu_PortOfDestination
                            .OrderBy(tlpod => tlpod.name), "Id", "name", permit.tbl_lu_PortOfDestination.Select(tlpod => tlpod.Id));
            ViewBag.SelectedShipmentAllowedByIds = new MultiSelectList(db.tbl_lu_ShipmentAllowedBy
                            .OrderBy(tlsab => tlsab.name), "Id", "name", permit.tbl_lu_ShipmentAllowedBy.Select(tlsab => tlsab.Id));
            ViewBag.SelectedIncotermIds = new MultiSelectList(db.tbl_lu_Incoterm
                            .OrderBy(tli => tli.name), "Id", "name", permit.tbl_lu_Incoterm.Select(tli => tli.Id));
            ViewBag.SelectedCountryOfOriginIds = new MultiSelectList(db.tbl_lu_CountryOfOrigin
                .OrderBy(tlc => tlc.name), "Id", "name", permit.tbl_lu_CountryOfOrigin.Select(tlcoo => tlcoo.Id));
            List<SelectListItem> approvalStatus = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NBE", Value = "NBE", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("NBE")
                },
                new SelectListItem {
                    Text = "Queue", Value = "Queue", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("Queue")
                },
                new SelectListItem {
                    Text = "Own Source", Value = "Own Source", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("Own Source")
                },
                new SelectListItem {
                    Text = "President", Value = "President", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("President")
                },
                new SelectListItem {
                    Text = "On Demand", Value = "On Demand", Selected = string.IsNullOrEmpty(permit.ApprovalStatus) ? false : permit.ApprovalStatus.Equals("On Demand")
                }
            };
            ViewBag.ApprovalStatus = approvalStatus;
            List<SelectListItem> ownSourceValue = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NRFCY", Value = "NRFCY", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("NRFCY")
                },
                new SelectListItem {
                    Text = "Retention", Value = "Retention", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("Retention")
                },
                new SelectListItem {
                    Text = "Diaspora", Value = "Diaspora", Selected = string.IsNullOrEmpty(permit.OwnSourceValue) ? false : permit.OwnSourceValue.Equals("Diaspora")
                },
            };
            ViewBag.OwnSourceValue = ownSourceValue;
            List<tblItemPriority> priorityList = db.tblItemPriorities.OrderBy(tip => tip.GroupBy).ToList();
            permit.firstPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("First Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.firstPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedFirstPriorityItem in permit.firstPriorityTopLevels)
                {
                    if (selectedFirstPriorityItem.Selected)
                    {
                        permit.selectedFirstPriorityTopLevels.Add(selectedFirstPriorityItem.Value);
                    }
                }
            }
            permit.secondPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Second Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.secondPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedSecondPriorityItem in permit.secondPriorityTopLevels)
                {
                    if (selectedSecondPriorityItem.Selected)
                    {
                        permit.selectedSecondPriorityTopLevels.Add(selectedSecondPriorityItem.Value);
                    }
                }
            }
            permit.thirdPriorityTopLevels = priorityList
                .Where(tip => tip.Priority.Equals("Third Priority"))
                .GroupBy(tip => tip.GroupBy)
                .Select(c => new SelectListItem()
                {
                    Text = c.FirstOrDefault().GroupBy,
                    Value = c.FirstOrDefault().GroupBy,
                    Selected = permit.tblItemPriorities.Any(tip => tip.GroupBy.Equals(c.FirstOrDefault().GroupBy))
                }).ToList();
            if (permit.thirdPriorityTopLevels != null)
            {
                foreach (SelectListItem selectedThirdPriorityItem in permit.thirdPriorityTopLevels)
                {
                    if (selectedThirdPriorityItem.Selected)
                    {
                        permit.selectedThirdPriorityTopLevels.Add(selectedThirdPriorityItem.Value);
                    }
                }
            }
            permit.firstPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("First Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.firstPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedFirstPriorityItem in permit.firstPrioritySubLevels)
                {
                    if (selectedFirstPriorityItem.Selected)
                    {
                        permit.selectedFirstPrioritySubLevels.Add(selectedFirstPriorityItem.Value);
                    }
                }
            }
            permit.secondPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Second Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.secondPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedSecondPriorityItem in permit.secondPrioritySubLevels)
                {
                    if (selectedSecondPriorityItem.Selected)
                    {
                        permit.selectedSecondPrioritySubLevels.Add(selectedSecondPriorityItem.Value);
                    }
                }
            }
            permit.thirdPrioritySubLevels = priorityList
               .Where(tip => permit.tblItemPriorities
               .Where(tip2 => tip2.Priority.Equals("Third Priority")).Select(tip2 => tip2.GroupBy)
               .Contains(tip.GroupBy))
               .OrderBy(tip => tip.GroupBy)
               .Select(c => new SelectListItem()
               {
                   Text = string.IsNullOrEmpty(c.Name)
                        ? c.GroupBy : c.GroupBy + "-" + c.Name,
                   Value = c.GroupBy + "-" + c.Name,
                   Selected = permit.tblItemPriorities.Any(tip => tip.Id.Equals(c.Id))
               }).ToList();
            if (permit.thirdPrioritySubLevels != null)
            {
                foreach (SelectListItem selectedThirdPriorityItem in permit.thirdPrioritySubLevels)
                {
                    if (selectedThirdPriorityItem.Selected)
                    {
                        permit.selectedThirdPrioritySubLevels.Add(selectedThirdPriorityItem.Value);
                    }
                }
            }
            //Some import permits do not have expiries, so check.
            bool expiryExists = permit.tblPOPermitExpiries.Any();
            if (expiryExists)
            {
                DateTime expiryDate = permit.tblPOPermitExpiries.FirstOrDefault(tppe => tppe.IsExtension == false).ExpiryDate;
                DateTime permitDate = permit.Date.Value.Date;
                permit.ExpiryDays = (expiryDate - permitDate).Days.ToString();
            }
            return permit;
        }
        public ActionResult EditImportPermit(Guid Id)
        {
            tblPermit permit = db.tblPermits.Find(Id);
            permit.ImporterName = permit.tblMerchant.ImporterName;
            permit.TinNumber = permit.tblMerchant.TinNumber;
            permit.NBENumber = permit.tblMerchant.NBENumber;
            return View(initEditImportPermitForm(permit));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditImportPermit(tblPermit permit)
        {
            if (string.IsNullOrEmpty(permit.LPCONumber))
            {
                ModelState.AddModelError("LPCONumber", "Required.");
            }
            if (string.IsNullOrEmpty(permit.ApprovalStatus))
            {
                ModelState.AddModelError("ApprovalStatus", "Required.");
            }
            else
            {
                if (permit.ApprovalStatus.Equals("NBE"))
                {
                    if (string.IsNullOrEmpty(permit.NBEApprovalRefNumber))
                    {
                        ModelState.AddModelError("NBEApprovalRefNumber", "Required.");
                    }
                }
                else if (permit.ApprovalStatus.Equals("Queue"))
                {
                    if (string.IsNullOrEmpty(permit.QueueRound))
                    {
                        ModelState.AddModelError("QueueRound", "Required.");
                    }
                    if (string.IsNullOrEmpty(permit.QueueNumber))
                    {
                        ModelState.AddModelError("QueueNumber", "Required.");
                    }
                }
                else if (permit.ApprovalStatus.Equals("Own Source"))
                {
                    if (string.IsNullOrEmpty(permit.OwnSourceValue))
                    {
                        ModelState.AddModelError("OwnSourceValue", "Required.");
                    }
                }
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            tblPermit _Permit = dbe.tblPermits.Find(permit.Id);
                            _Permit.LPCONumber = permit.LPCONumber;
                            _Permit.PermitStatusId = permit.PermitStatusId;
                            //clear approval status
                            _Permit.NonPriorityItems = permit.NonPriorityItems;
                            _Permit.tblItemPriorities.Clear();
                            _Permit.tbl_lu_PortOfLoading.Clear();
                            _Permit.tbl_lu_PortOfDestination.Clear();
                            _Permit.tbl_lu_ShipmentAllowedBy.Clear();
                            _Permit.tbl_lu_Incoterm.Clear();
                            _Permit.tbl_lu_CountryOfOrigin.Clear();
                            List<tbl_lu_PortOfLoading> portOfLoadings = dbe.tbl_lu_PortOfLoading.ToList();
                            List<tbl_lu_PortOfDestination> portOfDestinations = dbe.tbl_lu_PortOfDestination.ToList();
                            List<tbl_lu_ShipmentAllowedBy> shipmentAllowedBies = dbe.tbl_lu_ShipmentAllowedBy.ToList();
                            List<tbl_lu_Incoterm> incoterms = dbe.tbl_lu_Incoterm.ToList();
                            List<tbl_lu_CountryOfOrigin> countryOfOrigins = dbe.tbl_lu_CountryOfOrigin.ToList();
                            if (permit.SelectedPortOfLoadingIds != null)
                            {
                                foreach (Guid Id in permit.SelectedPortOfLoadingIds)
                                {
                                    _Permit.tbl_lu_PortOfLoading.Add(portOfLoadings.FirstOrDefault(pol => pol.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedPortOfDestinationIds != null)
                            {
                                foreach (Guid Id in permit.SelectedPortOfDestinationIds)
                                {
                                    _Permit.tbl_lu_PortOfDestination.Add(portOfDestinations.FirstOrDefault(pod => pod.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedShipmentAllowedByIds != null)
                            {
                                foreach (Guid Id in permit.SelectedShipmentAllowedByIds)
                                {
                                    _Permit.tbl_lu_ShipmentAllowedBy.Add(shipmentAllowedBies.FirstOrDefault(sab => sab.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedIncotermIds != null)
                            {
                                foreach (Guid Id in permit.SelectedIncotermIds)
                                {
                                    _Permit.tbl_lu_Incoterm.Add(incoterms.FirstOrDefault(i => i.Id.Equals(Id)));
                                }
                            }
                            if (permit.SelectedCountryOfOriginIds != null)
                            {
                                foreach (Guid Id in permit.SelectedCountryOfOriginIds)
                                {
                                    _Permit.tbl_lu_CountryOfOrigin.Add(countryOfOrigins.FirstOrDefault(coo => coo.Id.Equals(Id)));
                                }
                            }
                            List<tblItemPriority> itemPriorities = dbe.tblItemPriorities.ToList();
                            if (permit.selectedFirstPrioritySubLevels != null)
                            {
                                foreach (string selectedFirstPriorityItem in permit.selectedFirstPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("First Priority")
                                            && groupByName.Equals(selectedFirstPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            if (permit.selectedSecondPrioritySubLevels != null)
                            {
                                foreach (string selectedSecondPriorityItem in permit.selectedSecondPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("Second Priority")
                                            && groupByName.Equals(selectedSecondPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            if (permit.selectedThirdPrioritySubLevels != null)
                            {
                                foreach (string selectedThirdPriorityItem in permit.selectedThirdPrioritySubLevels)
                                {
                                    foreach (tblItemPriority itemPriority in itemPriorities)
                                    {
                                        string priority = itemPriority.Priority;
                                        string groupByName = itemPriority.GroupBy + "-" + itemPriority.Name;
                                        if (priority.Equals("Third Priority")
                                            && groupByName.Equals(selectedThirdPriorityItem))
                                        {
                                            _Permit.tblItemPriorities.Add(itemPriority);
                                        }
                                    }
                                }
                            }
                            //
                            if (permit.ApprovalStatus.Equals("NBE"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.NBEApprovalRefNumber = permit.NBEApprovalRefNumber;
                            }
                            else if (permit.ApprovalStatus.Equals("Queue"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.QueueRound = permit.QueueRound;
                                _Permit.QueueNumber = permit.QueueNumber;
                            }
                            else if (permit.ApprovalStatus.Equals("Own Source"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                                _Permit.OwnSourceValue = permit.OwnSourceValue;
                            }
                            else if (permit.ApprovalStatus.Equals("President")
                                || permit.ApprovalStatus.Equals("On Demand"))
                            {
                                _Permit.ApprovalStatus = permit.ApprovalStatus;
                            }
                            //Some import permits do not have expiries, so check.
                            tblPOPermitExpiry pOPermitExpiry = _Permit.tblPOPermitExpiries.FirstOrDefault(tppe => tppe.IsExtension == false);
                            if (pOPermitExpiry != null)
                            {
                                pOPermitExpiry.ExpiryDate = _Permit.Date.Value.AddDays(int.Parse(permit.ExpiryDays));
                            }
                            else
                            {
                                pOPermitExpiry = new tblPOPermitExpiry();
                                pOPermitExpiry.PermitId = _Permit.Id;
                                pOPermitExpiry.ExpiryDate = _Permit.Date.Value.AddDays(int.Parse(permit.ExpiryDays));
                                pOPermitExpiry.IsExtension = false;
                                dbe.tblPOPermitExpiries.Add(pOPermitExpiry);
                            }
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Merchant-EditImportPermit";
                            string object_id = _Permit.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Import permit successfully edited!";
                            return RedirectToAction("ImportPermits", new RouteValueDictionary(new { merchantId = _Permit.MerchantId }));
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            return View(initEditImportPermitFormError(permit));
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
