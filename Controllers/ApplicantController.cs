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
    public class ApplicantController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();
        private int sizeOfPage = 15;
        int numberOfPage = 1;

        public ActionResult Index(string applicantName, string CIFNumber, string permitNumber, int? page)
        {
            numberOfPage = (page ?? 1);
            var applicantList = db.tblApplicants.Where(ta => ta.Id != null);
            if (!string.IsNullOrEmpty(applicantName))
            {
                applicantList = applicantList.Where(ta => ta.ApplicantName.Contains(applicantName.Trim()));
            }
            if (!string.IsNullOrEmpty(CIFNumber))
            {
                applicantList = applicantList.Where(ta => ta.CIFNumber.Contains(CIFNumber.Trim()));
            }
            if (!string.IsNullOrEmpty(permitNumber))
            {
                applicantList = applicantList.Where(al => al.tblApplications.Any(ta => ta.PermitNumber.Contains(permitNumber.Trim())));
                ViewBag.permitNumber = permitNumber.Trim();
            }
            applicantList = applicantList.OrderBy(ta => ta.ApplicantName);
            return View(applicantList.ToPagedList(numberOfPage, sizeOfPage));
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(tblApplicant applicant)
        {
            if (string.IsNullOrEmpty(applicant.ApplicantName))
            {
                ModelState.AddModelError("ApplicantName", "Required.");
            }
            if (string.IsNullOrEmpty(applicant.CIFNumber))
            {
                ModelState.AddModelError("CIFNumber", "Required.");
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            bool cifNumberExists = dbe.tblApplicants
                                .Any(ta => ta.CIFNumber.Equals(applicant.CIFNumber.Trim()));
                            if (cifNumberExists)
                            {
                                ModelState.AddModelError("CIFNumber", "This CIF # is registered.");
                                return View(applicant);
                            }
                            tblApplicant _Applicant = new tblApplicant();
                            _Applicant.CIFNumber = applicant.CIFNumber.Trim();
                            _Applicant.ApplicantName = applicant.ApplicantName.Trim();
                            _Applicant.Remark = applicant.Remark;
                            dbe.tblApplicants.Add(_Applicant);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Applicant-Create";
                            string object_id = _Applicant.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Applicant created successfully!";
                            return RedirectToAction("Index");
                        }
                        catch (Exception exc)
                        {
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                            dbeTransaction.Rollback();
                        }
                    }
                }
            }
            return View(applicant);
        }

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
            catch (Exception) { }
            return Json(result ?? "", JsonRequestBehavior.AllowGet);
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

        public void initApplicantForm()
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
            List<SelectListItem> purposeOfPayment = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "Booking Fee", Value = "Booking Fee"
                },
                new SelectListItem {
                    Text = "Certification Fee", Value = "Certification Fee"
                },
                new SelectListItem
                {
                    Text = "Conference Fee", Value = "Conference Fee"
                },
                new SelectListItem
                {
                    Text = "Consultancy Fee", Value = "Consultancy Fee"
                },
                new SelectListItem
                {
                    Text = "Demurrage Payment", Value = "Demurrage Payment"
                },
                new SelectListItem {
                    Text = "Dividend Payment", Value = "Dividend Payment"
                },
                new SelectListItem
                {
                    Text = "Education Fees", Value = "Education Fees"
                },
                new SelectListItem
                {
                    Text = "Exhibition Fee", Value = "Exhibition Fee"
                },
                new SelectListItem
                {
                    Text = "Freight Payment", Value = "Freight Payment"
                },
                new SelectListItem {
                    Text = "Fund Transfer (Own Source)", Value = "Fund Transfer (Own Source)"
                },
                new SelectListItem
                {
                    Text = "IATA", Value = "IATA"
                },
                new SelectListItem
                {
                    Text = "Insurance Payment", Value = "Insurance Payment"
                },
                new SelectListItem
                {
                    Text = "License Fee", Value = "License Fee"
                },
                new SelectListItem {
                    Text = "Loan Payment", Value = "Loan Payment"
                },
                new SelectListItem
                {
                    Text = "Medical Fees", Value = "Medical Fees"
                },
                new SelectListItem
                {
                    Text = "Royalty Payment", Value = "Royalty Payment"
                },
                new SelectListItem
                {
                    Text = "Salary Payment", Value = "Salary Payment"
                },
                new SelectListItem {
                    Text = "Subscription Fee", Value = "Subscription Fee"
                },
                new SelectListItem
                {
                    Text = "Training Fee", Value = "Training Fee"
                },
            };
            ViewBag.PurposeOfPayment = purposeOfPayment;
        }

        public void initEditInvisiblePaymentPermitForm(tblApplication application)
        {
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")), "Id", "name", application.PermitStatusId);
            List<SelectListItem> approvalStatus = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NBE", Value = "NBE", Selected = application.ApprovalStatus.Equals("NBE")
                },
                new SelectListItem {
                    Text = "Queue", Value = "Queue", Selected = application.ApprovalStatus.Equals("Queue")
                },
                new SelectListItem {
                    Text = "Own Source", Value = "Own Source", Selected = application.ApprovalStatus.Equals("Own Source")
                },
                new SelectListItem {
                    Text = "President", Value = "President", Selected = application.ApprovalStatus.Equals("President")
                },
                new SelectListItem {
                    Text = "On Demand", Value = "On Demand", Selected = application.ApprovalStatus.Equals("On Demand")
                }
            };
            ViewBag.ApprovalStatus = approvalStatus;
            List<SelectListItem> ownSourceValue = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "NRFCY", Value = "NRFCY", Selected = string.IsNullOrEmpty(application.OwnSourceValue) ? false : application.OwnSourceValue.Equals("NRFCY")
                },
                new SelectListItem {
                    Text = "Retention", Value = "Retention", Selected = string.IsNullOrEmpty(application.OwnSourceValue) ? false : application.OwnSourceValue.Equals("Retention")
                },
                new SelectListItem {
                    Text = "Diaspora", Value = "Diaspora", Selected = string.IsNullOrEmpty(application.OwnSourceValue) ? false : application.OwnSourceValue.Equals("Diaspora")
                },
            };
            ViewBag.OwnSourceValue = ownSourceValue;
            List<SelectListItem> purposeOfPayment = new List<SelectListItem>() {
                new SelectListItem {
                    Text = "Booking Fee", Value = "Booking Fee", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Booking Fee") : false
                },
                new SelectListItem {
                    Text = "Certification Fee", Value = "Certification Fee", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Certification Fee") : false
                },
                new SelectListItem
                {
                    Text = "Conference Fee", Value = "Conference Fee", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Conference Fee") : false
                },
                new SelectListItem
                {
                    Text = "Consultancy Fee", Value = "Consultancy Fee", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Consultancy Fee") : false
                },
                new SelectListItem
                {
                    Text = "Demurrage Payment", Value = "Demurrage Payment", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Demurrage Payment") : false
                },
                new SelectListItem {
                    Text = "Dividend Payment", Value = "Dividend Payment", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Dividend Payment") : false
                },
                new SelectListItem
                {
                    Text = "Education Fees", Value = "Education Fees", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Education Fees") : false
                },
                new SelectListItem
                {
                    Text = "Exhibition Fee", Value = "Exhibition Fee", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Exhibition Fee") : false
                },
                new SelectListItem
                {
                    Text = "Freight Payment", Value = "Freight Payment", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Freight Payment") : false
                },
                new SelectListItem {
                    Text = "Fund Transfer (Own Source)", Value = "Fund Transfer (Own Source)", Selected  = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Fund Transfer (Own Source)") : false
                },
                new SelectListItem
                {
                    Text = "IATA", Value = "IATA", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("IATA") : false
                },
                new SelectListItem
                {
                    Text = "Insurance Payment", Value = "Insurance Payment", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Insurance Payment") : false
                },
                new SelectListItem
                {
                    Text = "License Fee", Value = "License Fee", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("License Fee") : false
                },
                new SelectListItem {
                    Text = "Loan Payment", Value = "Loan Payment", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Loan Payment") : false
                },
                new SelectListItem
                {
                    Text = "Medical Fees", Value = "Medical Fees", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Medical Fees") : false
                },
                new SelectListItem
                {
                    Text = "Royalty Payment", Value = "Royalty Payment", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Royalty Payment") : false
                },
                new SelectListItem
                {
                    Text = "Salary Payment", Value = "Salary Payment", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Salary Payment") : false
                },
                new SelectListItem {
                    Text = "Subscription Fee", Value = "Subscription Fee", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Subscription Fee") : false
                },
                new SelectListItem
                {
                    Text = "Training Fee", Value = "Training Fee", Selected = !string.IsNullOrEmpty(application.PurposeOfPayment) ? application.PurposeOfPayment.Equals("Training Fee") : false
                },
            };
            application.is_other = true;
            foreach (SelectListItem selectListItem in purposeOfPayment)
            {
                if (selectListItem.Selected)
                {
                    application.is_other = false;
                    break;
                }
            }
            if (application.is_other)
            {
                application.PurposeOfPaymentUserFill = application.PurposeOfPayment;
            }
            ViewBag.PurposeOfPayment = purposeOfPayment;
        }

        public decimal formatDecimal(string decimalValue)
        {
            decimalValue = decimalValue.Replace(",", "");
            return decimal.Parse(decimalValue);
        }

        public ActionResult EditInvisiblePaymentPermit(Guid Id)
        {
            tblApplication application = db.tblApplications.Find(Id);
            application.ApplicantName = application.tblApplicant.ApplicantName;
            application.CIFNumber = application.tblApplicant.CIFNumber;
            if (application.ApprovalStatus.Equals("NBE"))
            {
                application.NBEApprovalRefNumber = application.NBEApprovalRefNumber;
            }
            else if (application.ApprovalStatus.Equals("Queue"))
            {
                application.QueueRound = application.QueueRound;
                application.QueueNumber = application.QueueNumber;
            }
            else if (application.ApprovalStatus.Equals("Own Source"))
            {
                application.OwnSourceValue = application.OwnSourceValue;
            }
            else if (application.ApprovalStatus.Equals("President")
                || application.ApprovalStatus.Equals("On Demand"))
            {
                application.ApprovalStatus = application.ApprovalStatus;
            }
            initEditInvisiblePaymentPermitForm(application);
            return View(application);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditInvisiblePaymentPermit(tblApplication application)
        {
            if (application.is_other)
            {
                if (string.IsNullOrEmpty(application.PurposeOfPaymentUserFill))
                {
                    ModelState.AddModelError("PurposeOfPaymentUserFill", "Required.");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(application.PurposeOfPayment))
                {
                    ModelState.AddModelError("PurposeOfPayment", "Required.");
                }
            }
            if (string.IsNullOrEmpty(application.Beneficiary))
            {
                ModelState.AddModelError("Beneficiary", "Required.");
            }
            if (string.IsNullOrEmpty(application.ApprovalStatus))
            {
                ModelState.AddModelError("ApprovalStatus", "Required.");
            }
            else
            {
                if (application.ApprovalStatus.Equals("NBE"))
                {
                    if (string.IsNullOrEmpty(application.NBEApprovalRefNumber))
                    {
                        ModelState.AddModelError("NBEApprovalRefNumber", "Required.");
                    }
                }
                else if (application.ApprovalStatus.Equals("Queue"))
                {
                    if (string.IsNullOrEmpty(application.QueueRound))
                    {
                        ModelState.AddModelError("QueueRound", "Required.");
                    }
                    if (string.IsNullOrEmpty(application.QueueNumber))
                    {
                        ModelState.AddModelError("QueueNumber", "Required.");
                    }
                }
                else if (application.ApprovalStatus.Equals("Own Source"))
                {
                    if (string.IsNullOrEmpty(application.OwnSourceValue))
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
                            tblApplication _Application = dbe.tblApplications.Find(application.Id);
                            _Application.PermitStatusId = application.PermitStatusId;
                            if (application.is_other)
                            {
                                _Application.PurposeOfPayment = application.PurposeOfPaymentUserFill;
                            }
                            else
                            {
                                _Application.PurposeOfPayment = application.PurposeOfPayment;
                            }
                            _Application.Beneficiary = application.Beneficiary;
                            //clear approval status
                            _Application.ApprovalStatus = "";
                            _Application.NBEApprovalRefNumber = "";
                            _Application.ApprovalStatus = "";
                            _Application.QueueRound = "";
                            _Application.QueueNumber = "";
                            _Application.ApprovalStatus = "";
                            _Application.OwnSourceValue = "";
                            _Application.ApprovalStatus = "";
                            //
                            if (application.ApprovalStatus.Equals("NBE"))
                            {
                                _Application.ApprovalStatus = application.ApprovalStatus;
                                _Application.NBEApprovalRefNumber = application.NBEApprovalRefNumber;
                            }
                            else if (application.ApprovalStatus.Equals("Queue"))
                            {
                                _Application.ApprovalStatus = application.ApprovalStatus;
                                _Application.QueueRound = application.QueueRound;
                                _Application.QueueNumber = application.QueueNumber;
                            }
                            else if (application.ApprovalStatus.Equals("Own Source"))
                            {
                                _Application.ApprovalStatus = application.ApprovalStatus;
                                _Application.OwnSourceValue = application.OwnSourceValue;
                            }
                            else if (application.ApprovalStatus.Equals("President")
                                || application.ApprovalStatus.Equals("On Demand"))
                            {
                                _Application.ApprovalStatus = application.ApprovalStatus;
                            }
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Applicant-EditInvisiblePaymentPermit";
                            string object_id = _Application.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Invisible payment permit successfully edited!";
                            return RedirectToAction("InvisiblePaymentPermits", new RouteValueDictionary(new { applicantId = _Application.ApplicantId }));
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            initEditInvisiblePaymentPermitForm(application);
            return View(application);
        }
        public tblApplicationAmount initUpdateInvisiblePaymentPermitAmountRequestForm(Guid? Id)
        {
            tblApplication application = db.tblApplications.Find(Id);
            tblApplicationAmount applicationAmount = new tblApplicationAmount();
            applicationAmount.tblApplication = application;
            applicationAmount.ApplicationId = applicationAmount.tblApplication.Id;
            applicationAmount.ApplicantId = applicationAmount.tblApplication.ApplicantId;
            return applicationAmount;
        }
        public ActionResult UpdateInvisiblePaymentPermitAmountRequest(Guid? Id)
        {
            return View(initUpdateInvisiblePaymentPermitAmountRequestForm(Id));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateInvisiblePaymentPermitAmountRequest(tblApplicationAmount applicationAmount)
        {
            bool sendEmail = false;
            if (string.IsNullOrEmpty(applicationAmount.Reason))
            {
                ModelState.AddModelError("Reason", "Required.");
            }
            if (string.IsNullOrEmpty(applicationAmount.CurrencyRateValue)
                || applicationAmount.CurrencyRateValue.Equals("0"))
            {
                ModelState.AddModelError("CurrencyRateValue", "Required.");
            }
            if (string.IsNullOrEmpty(applicationAmount.AmountValue)
                || applicationAmount.AmountValue.Equals("0"))
            {
                ModelState.AddModelError("AmountValue", "Required.");
            }
            if (string.IsNullOrEmpty(applicationAmount.USDRateValue)
                || applicationAmount.USDRateValue.Equals("0"))
            {
                ModelState.AddModelError("USDRateValue", "Required.");
            }
            if (string.IsNullOrEmpty(applicationAmount.AmountInUSDValue)
                || applicationAmount.AmountInUSDValue.Equals("0"))
            {
                ModelState.AddModelError("AmountInUSDValue", "Required.");
            }
            if (string.IsNullOrEmpty(applicationAmount.AmountInBirrValue)
                || applicationAmount.AmountInBirrValue.Equals("0"))
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
                            tblApplicationAmount _ApplicationAmount = new tblApplicationAmount();
                            _ApplicationAmount.ApplicationId = applicationAmount.ApplicationId;
                            _ApplicationAmount.ApplicantId = applicationAmount.ApplicantId;
                            _ApplicationAmount.ApprovalStatusId = dbe.tbl_lu_Status.FirstOrDefault(tls => tls.name.Equals("Pending")).Id;
                            _ApplicationAmount.CurrencyRate = formatDecimal(applicationAmount.CurrencyRateValue);
                            _ApplicationAmount.Amount = formatDecimal(applicationAmount.AmountValue);
                            _ApplicationAmount.USDRate = formatDecimal(applicationAmount.USDRateValue);
                            _ApplicationAmount.AmountInUSD = formatDecimal(applicationAmount.AmountInUSDValue);
                            _ApplicationAmount.AmountInBirr = formatDecimal(applicationAmount.AmountInBirrValue);
                            _ApplicationAmount.CreatedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                            _ApplicationAmount.CreatedDate = DateTime.Now;
                            _ApplicationAmount.Reason = applicationAmount.Reason;
                            dbe.tblApplicationAmounts.Add(_ApplicationAmount);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Applicant-UpdateInvisiblePaymentPermitAmountRequest";
                            string object_id = _ApplicationAmount.Id.ToString();
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
                            int applicationPermitAmountUpdatePendingCount = db.tblApplicationAmounts
                                .Where(taa => taa.tbl_lu_Status.name.Equals("Pending")).Count();
                            string messageBody = "You have <span style=\"font-weight: bold; text-decoration: underline \">"
                                + applicationPermitAmountUpdatePendingCount + " application permit amount update</span> " +
                                "waiting approval.";
                            List<string> mailAddressList = dbe.USERS.Where(u => u
                            .ROLES.Any(r => r.RoleName.Equals("Manager")))
                                .Select(u => u.EMail + "#" + u.Firstname).ToList();
                            //mailAddressList.Clear();
                            new RBACUser().sendEmail(mailAddressList, messageBody, "Applicant/UpdateInvisiblePaymentPermitAmount_Auth");
                        }
                        return RedirectToAction("InvisiblePaymentPermits", new RouteValueDictionary(new { applicantId = applicationAmount.ApplicantId }));
                    }
                }
            }
            return View(initUpdateInvisiblePaymentPermitAmountRequestForm(applicationAmount.Id));
        }
        public ActionResult CreateInvisiblePaymentPermit(Guid Id)
        {
            tblApplication application = new tblApplication();
            tblApplicant applicant = db.tblApplicants.FirstOrDefault(ta => ta.Id.Equals(Id));
            application.ApplicantName = applicant.ApplicantName;
            application.CIFNumber = applicant.CIFNumber;
            application.ApplicantId = applicant.Id;
            initApplicantForm();
            return View(application);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateInvisiblePaymentPermit(tblApplication application)
        {
            if (application.is_other)
            {
                if (string.IsNullOrEmpty(application.PurposeOfPaymentUserFill))
                {
                    ModelState.AddModelError("PurposeOfPaymentUserFill", "Required.");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(application.PurposeOfPayment))
                {
                    ModelState.AddModelError("PurposeOfPayment", "Required.");
                }
            }
            if (string.IsNullOrEmpty(application.CurrencyRateValue)
            || application.CurrencyRateValue.Equals("0"))
            {
                ModelState.AddModelError("CurrencyRateValue", "Required.");
            }
            if (string.IsNullOrEmpty(application.AmountValue)
                || application.AmountValue.Equals("0"))
            {
                ModelState.AddModelError("AmountValue", "Required.");
            }
            if (string.IsNullOrEmpty(application.USDRateValue)
                || application.USDRateValue.Equals("0"))
            {
                ModelState.AddModelError("USDRateValue", "Required.");
            }
            if (string.IsNullOrEmpty(application.AmountInUSDValue)
                || application.AmountInUSDValue.Equals("0"))
            {
                ModelState.AddModelError("AmountInUSDValue", "Required.");
            }
            if (string.IsNullOrEmpty(application.AmountInBirrValue)
                || application.AmountInBirrValue.Equals("0"))
            {
                ModelState.AddModelError("AmountInBirrValue", "Required.");
            }
            if (string.IsNullOrEmpty(application.CurrencyType))
            {
                ModelState.AddModelError("CurrencyType", "Required.");
            }
            if (string.IsNullOrEmpty(application.Beneficiary))
            {
                ModelState.AddModelError("Beneficiary", "Required.");
            }
            if (string.IsNullOrEmpty(application.ApprovalStatus))
            {
                ModelState.AddModelError("ApprovalStatus", "Required.");
            }
            else
            {
                if (application.ApprovalStatus.Equals("NBE"))
                {
                    if (string.IsNullOrEmpty(application.NBEApprovalRefNumber))
                    {
                        ModelState.AddModelError("NBEApprovalRefNumber", "Required.");
                    }
                }
                else if (application.ApprovalStatus.Equals("Queue"))
                {
                    if (string.IsNullOrEmpty(application.QueueRound))
                    {
                        ModelState.AddModelError("QueueRound", "Required.");
                    }
                    if (string.IsNullOrEmpty(application.QueueNumber))
                    {
                        ModelState.AddModelError("QueueNumber", "Required.");
                    }
                }
                else if (application.ApprovalStatus.Equals("Own Source"))
                {
                    if (string.IsNullOrEmpty(application.OwnSourceValue))
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
                            tblApplication _Application = new tblApplication();
                            _Application.CreatedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                            _Application.ApplicantId = application.ApplicantId;
                            int nextSerialNumberValue = returnNextSerialNumberValueByType(dbe, "IP", currentYear);
                            tblSerialNumberShelf prevSerial = dbe.tblSerialNumberShelves
                                .FirstOrDefault(tsns => tsns.SerialNumberType.Equals("IP")
                                && tsns.IsLatest == true);
                            prevSerial.IsLatest = false;
                            dbe.SaveChanges();
                            tblSerialNumberShelf serialNumberShelf = new tblSerialNumberShelf();
                            serialNumberShelf.SerialNumberType = "IP";
                            serialNumberShelf.SerialNumberValue = nextSerialNumberValue;
                            serialNumberShelf.IsLatest = true;
                            serialNumberShelf.Year = currentYear;
                            dbe.tblSerialNumberShelves.Add(serialNumberShelf);
                            dbe.SaveChanges();
                            _Application.SerialNumberShelfId = serialNumberShelf.Id;
                            _Application.PermitType = "03";
                            _Application.PermitYear = currentYear;
                            _Application.Date = DateTime.Now;
                            _Application.CreatedDate = DateTime.Now;
                            _Application.PermitStatusId = application.PermitStatusId;
                            _Application.PermitNumber = "ZEB-ZBH-03-"
                                + serialNumberShelf.SerialNumberValue.ToString().PadLeft(5, '0')
                                + "-" + _Application.PermitYear;
                            _Application.CurrencyType = application.CurrencyType;
                            _Application.CurrencyRate = formatDecimal(application.CurrencyRateValue);
                            _Application.Amount = formatDecimal(application.AmountValue);
                            _Application.RemainingAmount = _Application.Amount;
                            _Application.USDRate = formatDecimal(application.USDRateValue);
                            _Application.AmountInUSD = formatDecimal(application.AmountInUSDValue);
                            _Application.RemainingAmountInUSD = _Application.AmountInUSD;
                            _Application.AmountInBirr = formatDecimal(application.AmountInBirrValue);
                            _Application.RemainingAmountInBirr = _Application.AmountInBirr;
                            if (application.is_other)
                            {
                                _Application.PurposeOfPayment = application.PurposeOfPaymentUserFill;
                            }
                            else
                            {
                                _Application.PurposeOfPayment = application.PurposeOfPayment;
                            }
                            _Application.Beneficiary = application.Beneficiary;
                            if (application.ApprovalStatus.Equals("NBE"))
                            {
                                _Application.ApprovalStatus = application.ApprovalStatus;
                                _Application.NBEApprovalRefNumber = application.NBEApprovalRefNumber;
                            }
                            else if (application.ApprovalStatus.Equals("Queue"))
                            {
                                _Application.ApprovalStatus = application.ApprovalStatus;
                                _Application.QueueRound = application.QueueRound;
                                _Application.QueueNumber = application.QueueNumber;
                            }
                            else if (application.ApprovalStatus.Equals("Own Source"))
                            {
                                _Application.ApprovalStatus = application.ApprovalStatus;
                                _Application.OwnSourceValue = application.OwnSourceValue;
                            }
                            else if (application.ApprovalStatus.Equals("President")
                                || application.ApprovalStatus.Equals("On Demand"))
                            {
                                _Application.ApprovalStatus = application.ApprovalStatus;
                            }
                            dbe.tblApplications.Add(_Application);
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Applicant-CreateInvisiblePaymentPermit";
                            string object_id = _Application.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Invisible payment permit successfully created!";
                            return RedirectToAction("InvisiblePaymentPermitConfirmation", new RouteValueDictionary(new { Id = _Application.Id }));
                        }
                        catch (Exception exc)
                        {
                            dbeTransaction.Rollback();
                            TempData["sErrMsg"] = "Unknown error occured. Please try again.";
                        }
                    }
                }
            }
            initApplicantForm();
            return View(application);
        }
        public ActionResult InvisiblePaymentPermitConfirmation(Guid Id)
        {
            tblApplication application = db.tblApplications.FirstOrDefault(ta => ta.Id.Equals(Id));
            if (application.ApprovalStatus.Equals("NBE"))
            {
                application.NBEApprovalRefNumber = application.NBEApprovalRefNumber;
            }
            else if (application.ApprovalStatus.Equals("Queue"))
            {
                application.QueueRound = application.QueueRound;
                application.QueueNumber = application.QueueNumber;
            }
            else if (application.ApprovalStatus.Equals("Own Source"))
            {
                application.OwnSourceValue = application.OwnSourceValue;
            }
            else if (application.ApprovalStatus.Equals("President")
                || application.ApprovalStatus.Equals("On Demand"))
            {
                application.ApprovalStatus = application.ApprovalStatus;
            }
            return View(application);
        }
        public ActionResult InvisiblePaymentPermits(int? page, Guid? applicantId, string permitNumber)
        {
            numberOfPage = (page ?? 1);
            if (string.IsNullOrEmpty(permitNumber))
            {
                if (applicantId.HasValue)
                {
                    var applicationList = db.tblApplications.Where(ta => ta.ApplicantId.Equals(applicantId.Value)
                    && ta.tblSerialNumberShelf.SerialNumberType.Equals("IP"))
                        .OrderByDescending(ta => ta.Date);
                    tblApplicant applicant = db.tblApplicants.Find(applicantId.Value);
                    ViewBag.ApplicantName = applicant.ApplicantName;
                    ViewBag.CIFNumber = applicant.CIFNumber;
                    ViewBag.applicantId = applicant.Id;
                    return View(applicationList.ToPagedList(numberOfPage, sizeOfPage));
                }
                else
                {
                    TempData["sErrMsg"] = "Enter Permit #.";
                    return RedirectToAction("Index", new RouteValueDictionary(new { permitNumber = permitNumber }));
                }
            }
            else
            {
                var permitCount = db.tblApplications.Where(ta => ta.PermitNumber.Contains(permitNumber)).Count();
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
                    var applicationList = db.tblApplications.Where(ta => ta.PermitNumber.Contains(permitNumber))
                        .Take(1).OrderByDescending(ta => ta.Date);
                    tblApplicant applicant = applicationList.ToList().FirstOrDefault().tblApplicant;
                    ViewBag.ApplicantName = applicant.ApplicantName;
                    ViewBag.CIFNumber = applicant.CIFNumber;
                    ViewBag.applicantId = applicant.Id;
                    ViewBag.permitNumber = permitNumber;
                    return View(applicationList.ToPagedList(numberOfPage, sizeOfPage));
                }
            }
        }

        public ActionResult UpdateInvisiblePaymentPermitAmount_Auth(int? page)
        {
            numberOfPage = (page ?? 1);
            var applicationAmountList = db.tblApplicationAmounts
                .Where(taa => taa.tbl_lu_Status.name.Equals("Pending"))
                .OrderByDescending(taa => taa.CreatedDate);
            return View(applicationAmountList.ToPagedList(numberOfPage, sizeOfPage));
        }
        public ActionResult ViewInvisiblePaymentPermitApplicationDetail(Guid Id)
        {
            tblApplication application = db.tblApplications.FirstOrDefault(ta => ta.Id.Equals(Id));
            return PartialView(application);
        }
        public ActionResult ViewApplicantDetail(Guid Id)
        {
            tblApplicant applicant = db.tblApplicants.Find(Id);
            return PartialView(applicant);
        }
        public ActionResult ViewIPPermitAmountUpdateRejectionComment(Guid Id)
        {
            tblApplicationAmount applicationAmount = db.tblApplicationAmounts.Find(Id);
            return PartialView(applicationAmount);
        }

        public ActionResult ViewInvisiblePaymentPermitDetail(Guid Id)
        {
            tblApplicationAmount applicationAmount = db.tblApplicationAmounts
                        .FirstOrDefault(taa => taa.Id.Equals(Id));
            return PartialView(applicationAmount);
        }

        public ActionResult ViewInvisiblePaymentPermitAmountUpdatesDetail(Guid Id)
        {
            var applicationAmountList = db.tblApplicationAmounts
                        .Where(taa => taa.ApplicationId.Equals(Id));
            return PartialView(applicationAmountList);
        }


        public ActionResult Edit(Guid? Id)
        {
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            tblApplicant applicant = db.tblApplicants.Find(Id);
            applicant.OldCIFNumber = applicant.CIFNumber;
            if (applicant == null)
            {
                return HttpNotFound();
            }
            return View(applicant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(tblApplicant applicant)
        {
            if (string.IsNullOrEmpty(applicant.CIFNumber))
            {
                ModelState.AddModelError("CIFNumber", "Required.");
                return View(applicant);
            }
            if (string.IsNullOrEmpty(applicant.ApplicantName))
            {
                ModelState.AddModelError("ApplicantName", "Required.");
                return View(applicant);
            }
            if (ModelState.IsValid)
            {
                using (var dbe = new ZB_FEPMS_Model())
                {
                    using (var dbeTransaction = dbe.Database.BeginTransaction())
                    {
                        try
                        {
                            if (!applicant.OldCIFNumber.Equals(applicant.CIFNumber.Trim()))
                            {
                                bool cifNumberExists = dbe.tblApplicants
                                .Any(ta => ta.CIFNumber.Equals(applicant.CIFNumber.Trim()));
                                if (cifNumberExists)
                                {
                                    ModelState.AddModelError("CIFNumber", "This CIF # is registered.");
                                    return View(applicant);
                                }
                            }
                            tblApplicant _Applicant = dbe.tblApplicants.Find(applicant.Id);
                            _Applicant.CIFNumber = applicant.CIFNumber.Trim();
                            _Applicant.ApplicantName = applicant.ApplicantName;
                            _Applicant.Remark = applicant.Remark;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Applicant-Edit";
                            string object_id = _Applicant.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Applicant successfully edited!";
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
            return View(applicant);
        }

        public ActionResult ApproveInvisiblePaymentPermitAmountUpdate(Guid Id)
        {
            tblApplicationAmount applicationAmount = db.tblApplicationAmounts.Find(Id);
            if (applicationAmount == null)
            {
                return HttpNotFound();
            }
            return View(applicationAmount);
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
                        tblApplicationAmount applicationAmount = dbe.tblApplicationAmounts
                            .FirstOrDefault(taa => taa.Id.Equals(Id));
                        applicationAmount.ApprovalStatusId = dbe.tbl_lu_Status
                            .FirstOrDefault(tls => tls.name.Equals("Approved")).Id;
                        applicationAmount.ApprovedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                        applicationAmount.ApprovedDate = DateTime.Now;
                        dbe.SaveChanges();
                        tblApplication application = dbe.tblApplications.Find(applicationAmount.ApplicationId);
                        application.RemainingAmount += applicationAmount.Amount;
                        application.RemainingAmountInUSD += applicationAmount.AmountInUSD;
                        application.RemainingAmountInBirr += applicationAmount.AmountInBirr;
                        //Set increased and decreased amounts here
                        if (applicationAmount.Amount > 0
                            && applicationAmount.AmountInUSD > 0
                            && applicationAmount.AmountInBirr > 0)
                        {
                            if (application.IncreasedAmount.HasValue && application.IncreasedAmountInUSD.HasValue && application.IncreasedAmountInBirr.HasValue)
                            {
                                application.IncreasedAmount += applicationAmount.Amount;
                                application.IncreasedAmountInUSD += applicationAmount.AmountInUSD;
                                application.IncreasedAmountInBirr += applicationAmount.AmountInBirr;
                            }
                            else if (!application.IncreasedAmount.HasValue && !application.IncreasedAmountInUSD.HasValue && !application.IncreasedAmountInBirr.HasValue)
                            {
                                application.IncreasedAmount = applicationAmount.Amount;
                                application.IncreasedAmountInUSD = applicationAmount.AmountInUSD;
                                application.IncreasedAmountInBirr = applicationAmount.AmountInBirr;
                            }
                        }
                        else if (applicationAmount.Amount < 0
                            && applicationAmount.AmountInUSD < 0
                            && applicationAmount.AmountInBirr < 0)
                        {
                            if (application.DecreasedAmount.HasValue && application.DecreasedAmountInUSD.HasValue && application.DecreasedAmountInBirr.HasValue)
                            {
                                application.DecreasedAmount += applicationAmount.Amount;
                                application.DecreasedAmountInUSD += applicationAmount.AmountInUSD;
                                application.DecreasedAmountInBirr += applicationAmount.AmountInBirr;
                            }
                            else if (!application.DecreasedAmount.HasValue && !application.DecreasedAmountInUSD.HasValue && !application.DecreasedAmountInBirr.HasValue)
                            {
                                application.DecreasedAmount = applicationAmount.Amount;
                                application.DecreasedAmountInUSD = applicationAmount.AmountInUSD;
                                application.DecreasedAmountInBirr = applicationAmount.AmountInBirr;
                            }
                        }
                        dbe.SaveChanges();
                        RBACUser rbacUserObj = new RBACUser();
                        string operation = "Applicant-Approve";
                        string object_id = applicationAmount.Id.ToString();
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
            return RedirectToAction("UpdateInvisiblePaymentPermitAmount_Auth");
        }

        public ActionResult Reject(Guid Id)
        {
            tblApplicationAmount applicationAmount = db.tblApplicationAmounts.Find(Id);
            if (applicationAmount == null)
            {
                return HttpNotFound();
            }
            return View(applicationAmount);
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
                            tblApplicationAmount applicationAmount = dbe.tblApplicationAmounts
                            .FirstOrDefault(taa => taa.Id.Equals(Id));
                            applicationAmount.ApprovalStatusId = dbe.tbl_lu_Status
                                .FirstOrDefault(tls => tls.name.Equals("Rejected")).Id;
                            applicationAmount.ApprovedBy = Guid.Parse(System.Web.HttpContext.Current.Session["userIdAttribute"].ToString());
                            applicationAmount.ApprovedDate = DateTime.Now;
                            applicationAmount.Remark = Remark;
                            dbe.SaveChanges();
                            RBACUser rbacUserObj = new RBACUser();
                            string operation = "Applicant-Reject";
                            string object_id = applicationAmount.Id.ToString();
                            rbacUserObj.saveActivityLog(dbe, operation, object_id);
                            dbeTransaction.Commit();
                            TempData["successMsg"] = "Successfully rejected!";
                            return RedirectToAction("UpdateInvisiblePaymentPermitAmount_Auth");
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
