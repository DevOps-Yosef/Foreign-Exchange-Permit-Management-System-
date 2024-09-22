using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using ZB_FEPMS.Action_Filters;
using ZB_FEPMS.Models;

//$$

namespace ZB_FEPMS.Controllers
{

    [RBAC]
    [NoCache]
    public class PermitReportController : Controller
    {
        private ZB_FEPMS_Model db = new ZB_FEPMS_Model();

        public NBEReportForm initNBEReportForm()
        {
            NBEReportForm reportForm = new NBEReportForm();
            ViewBag.MethodOfPaymentId = new SelectList(db.tbl_lu_MethodOfPayment
                .OrderBy(tlc => tlc.name), "Id", "name");
            List<string> currencyTypeList = db.tblPermits.Select(tp => tp.CurrencyType).Distinct().ToList();
            List<SelectListItem> currencyTypes = new List<SelectListItem>();
            foreach (string currencyType in currencyTypeList)
            {
                SelectListItem selectListItem = new SelectListItem();
                selectListItem.Text = currencyType;
                selectListItem.Value = currencyType;
                currencyTypes.Add(selectListItem);
            }
            ViewBag.CurrencyType = currencyTypes;
            return reportForm;
        }
        public ActionResult NBEReport()
        {
            return View(initNBEReportForm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult NBEReport(NBEReportForm reportForm)
        {
            if (reportForm.dateFrom > reportForm.dateTo)
            {
                TempData["sErrMsg"] = "Date to should be after date from.";
            }
            else
            {
                if (ModelState.IsValid)
                {
                    Nullable<Guid> method_of_payment_id = reportForm.MethodOfPaymentId;
                    string currency_type = reportForm.CurrencyType;
                    List<NBEReport> NBEReports = new List<NBEReport>();
                    ReportDocument reportDocument = new ReportDocument();
                    reportDocument.Load(Path.Combine(Server.MapPath("~/Reports"), "NBEReport.rpt"));
                    var NBEReportQuery = db.tblPermits
                        .Where(tp => tp.tblSerialNumberShelf.SerialNumberType.Equals("IMP")
                        && tp.tbl_lu_Status.name.Equals("Active")
                    && tp.CreatedDate >= reportForm.dateFrom
                    && tp.CreatedDate <= reportForm.dateTo);
                    if (reportForm.MethodOfPaymentId.HasValue)
                    {
                        NBEReportQuery = NBEReportQuery
                            .Where(nrq => nrq.MethodOfPaymentId.Value.Equals(method_of_payment_id.Value));
                    }
                    if (!string.IsNullOrEmpty(currency_type))
                    {
                        NBEReportQuery = NBEReportQuery
                            .Where(nrq => nrq.CurrencyType.Equals(currency_type));
                    }
                    NBEReports = NBEReportQuery.OrderBy(nr => nr.Date).Select(nr => new NBEReport()
                    {
                        Date = nr.Date.Value,
                        NBENumber = nr.tblMerchant.NBENumber,
                        PermitNumber = nr.PermitNumber,
                        ImporterName = nr.tblMerchant.ImporterName,
                        MethodOfPayment = nr.tbl_lu_MethodOfPayment.name,
                        CurrencyType = nr.CurrencyType,
                        Amount = nr.Amount,
                        CurrencyRate = nr.CurrencyRate.Value,
                        AmountInBirr = nr.AmountInBirr.Value,
                        LPCONumber = nr.LPCONumber,
                    }).ToList();
                    if (NBEReports.Count == 0)
                    {
                        TempData["sErrMsg"] = "There is no data for your filter.";
                    }
                    else
                    {
                        reportDocument.SetDataSource(NBEReports);
                        string reportDate = reportForm.dateFrom.ToString("dd/MMM/yyyy") + " to " + reportForm.dateTo.ToString("dd/MMM/yyyy");
                        reportDocument.SetParameterValue("ReportTitle1", "BANK NAME…ZEMEN BANK S.C.");
                        reportDocument.SetParameterValue("ReportTitle2", "WEEKLY REPORT " + reportDate.ToUpper());
                        reportDocument.SetParameterValue("ReportTitle3", "IMPORT PERMIT ISUED FOR THE PERIOD " + reportDate.ToUpper());
                        Response.Buffer = false;
                        Response.ClearContent();
                        Response.ClearHeaders();
                        ExportOptions exportOpts = new ExportOptions();
                        exportOpts.ExportFormatType = ExportFormatType.Excel;
                        ExcelFormatOptions exportFormatOptions = ExportOptions.CreateExcelFormatOptions();
                        exportFormatOptions.ShowGridLines = true;
                        exportFormatOptions.ExcelUseConstantColumnWidth = false;
                        exportOpts.ExportFormatOptions = exportFormatOptions;
                        reportDocument.ExportToHttpResponse(exportOpts, System.Web.HttpContext.Current.Response, false, "NBE Report");
                        return RedirectToAction("NBEReport");
                    }
                }
            }
            return View(initNBEReportForm());
        }


        public PurchaseOrderPermitMasterReportForm initPurchaseOrderPermitMasterReportForm()
        {
            PurchaseOrderPermitMasterReportForm purchaseOrderPermitMasterReportForm = new PurchaseOrderPermitMasterReportForm();
            ViewBag.MerchantId = new SelectList(db.tblMerchants
                .OrderBy(tm => tm.ImporterName), "Id", "ImporterName");
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")),
                "Id", "name");
            List<string> currencyTypeList = db.tblPermits.Select(tp => tp.CurrencyType).Distinct().ToList();
            List<SelectListItem> currencyTypes = new List<SelectListItem>();
            foreach (string currencyType in currencyTypeList)
            {
                SelectListItem selectListItem = new SelectListItem();
                selectListItem.Text = currencyType;
                selectListItem.Value = currencyType;
                currencyTypes.Add(selectListItem);
            }
            ViewBag.CurrencyType = currencyTypes;
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
            return purchaseOrderPermitMasterReportForm;
        }

        public ActionResult PurchaseOrderPermitMasterReport()
        {
            return View(initPurchaseOrderPermitMasterReportForm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PurchaseOrderPermitMasterReport(PurchaseOrderPermitMasterReportForm purchaseOrderPermitMasterReportForm)
        {
            if (purchaseOrderPermitMasterReportForm.dateFrom > purchaseOrderPermitMasterReportForm.dateTo)
            {
                TempData["sErrMsg"] = "Date to should be after date from.";
            }
            else
            {
                if (ModelState.IsValid)
                {
                    Nullable<Guid> merchant_id = purchaseOrderPermitMasterReportForm.MerchantId;
                    Nullable<Guid> permit_status_id = purchaseOrderPermitMasterReportForm.PermitStatusId;
                    string currency_type = purchaseOrderPermitMasterReportForm.CurrencyType;
                    string approval_status = purchaseOrderPermitMasterReportForm.ApprovalStatus;
                    List<PurchaseOrderPermitMasterReport> purchaseOrderPermitMasterReports = new List<PurchaseOrderPermitMasterReport>();
                    var POPermitMasterReportQuery = db.tblPermits
                        .Where(tp => tp.tblSerialNumberShelf.SerialNumberType.Equals("PO")
                        && tp.CreatedDate >= purchaseOrderPermitMasterReportForm.dateFrom
                        && tp.CreatedDate <= purchaseOrderPermitMasterReportForm.dateTo);
                    if (purchaseOrderPermitMasterReportForm.MerchantId.HasValue)
                    {
                        POPermitMasterReportQuery = POPermitMasterReportQuery
                            .Where(popmrq => popmrq.MerchantId.Equals(merchant_id.Value));
                    }
                    if (purchaseOrderPermitMasterReportForm.PermitStatusId.HasValue)
                    {
                        POPermitMasterReportQuery = POPermitMasterReportQuery
                            .Where(popmrq => popmrq.PermitStatusId.Value.Equals(permit_status_id.Value));
                    }
                    if (!string.IsNullOrEmpty(currency_type))
                    {
                        POPermitMasterReportQuery = POPermitMasterReportQuery
                            .Where(popmrq => popmrq.CurrencyType.Equals(currency_type));
                    }
                    if (!string.IsNullOrEmpty(approval_status))
                    {
                        POPermitMasterReportQuery = POPermitMasterReportQuery
                            .Where(popmrq => popmrq.ApprovalStatus.Equals(approval_status));
                    }
                    purchaseOrderPermitMasterReports = POPermitMasterReportQuery.OrderBy(popmrq => popmrq.Date)
                        .ToList()
                    .Select(popmrq => new PurchaseOrderPermitMasterReport()
                    {
                        Date = popmrq.Date.Value,
                        NBENumber = popmrq.tblMerchant.NBENumber,
                        PermitNumber = popmrq.PermitNumber,
                        ImporterName = popmrq.tblMerchant.ImporterName,
                        PermitStatus = popmrq.tbl_lu_Status.name,
                        CurrencyType = popmrq.CurrencyType,
                        Amount = popmrq.Amount,
                        CurrencyRate = popmrq.CurrencyRate.HasValue ? popmrq.CurrencyRate.Value : 0,
                        AmountInBirr = popmrq.AmountInBirr.HasValue ? popmrq.AmountInBirr.Value : 0,
                        USDRate = popmrq.USDRate.HasValue ? popmrq.USDRate.Value : 0,
                        AmountInUSD = popmrq.AmountInUSD.HasValue ? popmrq.AmountInUSD.Value : 0,
                        RemainingAmount = popmrq.RemainingAmount.HasValue ? popmrq.RemainingAmount.Value : 0,
                        RemainingAmountInUSD = popmrq.RemainingAmountInUSD.HasValue ? popmrq.RemainingAmountInUSD.Value : 0,
                        RemainingAmountInBirr = popmrq.RemainingAmountInBirr.HasValue ? popmrq.RemainingAmountInBirr.Value : 0,
                        LPCONumber = popmrq.LPCONumber,
                        PortOfLoading = string.Join(", ", popmrq.tbl_lu_PortOfLoading.ToList().Select(tlpod => tlpod.name)),
                        PortOfDestination = string.Join(", ", popmrq.tbl_lu_PortOfDestination.ToList().Select(tlpod => tlpod.name)),
                        ShipmentAllowedBy = string.Join(", ", popmrq.tbl_lu_ShipmentAllowedBy.ToList().Select(tlpod => tlpod.name)),
                        Incoterm = string.Join(", ", popmrq.tbl_lu_Incoterm.ToList().Select(tlpod => tlpod.name)),
                        CountryOfOrigin = string.Join(", ", popmrq.tbl_lu_CountryOfOrigin.ToList().Select(tlpod => tlpod.name)),
                        FirstPriorityItems = string.Join(", ", popmrq.tblItemPriorities.Where(tip => tip.Priority.Equals("First Priority"))
                        .ToList().Select(tip => tip.GroupBy + (!string.IsNullOrEmpty(tip.Name) ? ("-" + tip.Name) : ""))),
                        SecondPriorityItems = string.Join(", ", popmrq.tblItemPriorities.Where(tip => tip.Priority.Equals("Second Priority"))
                        .ToList().Select(tip => tip.GroupBy + (!string.IsNullOrEmpty(tip.Name) ? ("-" + tip.Name) : ""))),
                        ThirdPriorityItems = string.Join(", ", popmrq.tblItemPriorities.Where(tip => tip.Priority.Equals("Third Priority"))
                        .ToList().Select(tip => tip.GroupBy + (!string.IsNullOrEmpty(tip.Name) ? ("-" + tip.Name) : ""))),
                        NonPriorityItems = popmrq.NonPriorityItems,
                        ApprovalStatus = popmrq.ApprovalStatus,
                        NBEApprovalRefNumber = popmrq.NBEApprovalRefNumber,
                        QueueRound = popmrq.QueueRound,
                        QueueNumber = popmrq.QueueNumber,
                        OwnSourceValue = popmrq.OwnSourceValue,
                        IncreasedAmount = popmrq.IncreasedAmount.HasValue ? popmrq.IncreasedAmount.Value : 0,
                        IncreasedAmountInUSD = popmrq.IncreasedAmountInUSD.HasValue ? popmrq.IncreasedAmountInUSD.Value : 0,
                        IncreasedAmountInBirr = popmrq.IncreasedAmountInBirr.HasValue ? popmrq.IncreasedAmountInBirr.Value : 0,
                        DecreasedAmount = popmrq.DecreasedAmount.HasValue ? popmrq.DecreasedAmount.Value : 0,
                        DecreasedAmountInUSD = popmrq.DecreasedAmountInUSD.HasValue ? popmrq.DecreasedAmountInUSD.Value : 0,
                        DecreasedAmountInBirr = popmrq.DecreasedAmountInBirr.HasValue ? popmrq.CurrencyRate.Value : 0,
                        PreparedBy = popmrq.USER != null ? (popmrq.USER.Firstname + " " + popmrq.USER.Lastname) : "",
                        ExpiryDate = popmrq.tblPOPermitExpiries.OrderByDescending(tpope => tpope.ExpiryDate).FirstOrDefault().ExpiryDate
                    }).ToList();
                    if (purchaseOrderPermitMasterReports.Count == 0)
                    {
                        TempData["sErrMsg"] = "There is no data for your filter.";
                    }
                    else
                    {
                        ReportDocument reportDocument = new ReportDocument();
                        reportDocument.Load(Path.Combine(Server.MapPath("~/Reports"), "PurchaseOrderPermitMasterReport.rpt"));
                        reportDocument.SetDataSource(purchaseOrderPermitMasterReports);
                        string reportDate = purchaseOrderPermitMasterReportForm.dateFrom.ToString("dd/MMM/yyyy")
                            + " to " + purchaseOrderPermitMasterReportForm.dateTo.ToString("dd/MMM/yyyy");
                        reportDocument.SetParameterValue("ReportTitle1", "ZEMEN BANK S.C.");
                        reportDocument.SetParameterValue("ReportTitle2", "Purchase Order Permit Master Report ".ToUpper() + reportDate.ToUpper());
                        Response.Buffer = false;
                        Response.ClearContent();
                        Response.ClearHeaders();
                        ExportOptions exportOpts = new ExportOptions();
                        exportOpts.ExportFormatType = ExportFormatType.Excel;
                        ExcelFormatOptions exportFormatOptions = ExportOptions.CreateExcelFormatOptions();
                        exportFormatOptions.ShowGridLines = true;
                        exportFormatOptions.ExcelUseConstantColumnWidth = false;
                        exportOpts.ExportFormatOptions = exportFormatOptions;
                        reportDocument.ExportToHttpResponse(exportOpts, System.Web.HttpContext.Current.Response, false, "Purchase Order Permit Master Report");
                        return RedirectToAction("PurchaseOrderPermitMasterReport");
                    }
                }
            }
            return View(initPurchaseOrderPermitMasterReportForm());
        }

        public ImportPermitMasterReportForm initImportPermitMasterReportForm()
        {
            ImportPermitMasterReportForm importPermitMasterReportForm = new ImportPermitMasterReportForm();
            ViewBag.MerchantId = new SelectList(db.tblMerchants
                .OrderBy(tm => tm.ImporterName), "Id", "ImporterName");
            ViewBag.MethodOfPaymentId = new SelectList(db.tbl_lu_MethodOfPayment
                .OrderBy(tlc => tlc.name), "Id", "name");
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")),
                "Id", "name");
            List<string> currencyTypeList = db.tblPermits.Select(tp => tp.CurrencyType).Distinct().ToList();
            List<SelectListItem> currencyTypes = new List<SelectListItem>();
            foreach (string currencyType in currencyTypeList)
            {
                SelectListItem selectListItem = new SelectListItem();
                selectListItem.Text = currencyType;
                selectListItem.Value = currencyType;
                currencyTypes.Add(selectListItem);
            }
            ViewBag.CurrencyType = currencyTypes;
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
            return importPermitMasterReportForm;
        }

        public ActionResult ImportPermitMasterReport()
        {
            return View(initImportPermitMasterReportForm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ImportPermitMasterReport(ImportPermitMasterReportForm importPermitMasterReportForm)
        {
            if (importPermitMasterReportForm.dateFrom > importPermitMasterReportForm.dateTo)
            {
                TempData["sErrMsg"] = "Date to should be after date from.";
            }
            else
            {
                if (ModelState.IsValid)
                {
                    Nullable<Guid> merchant_id = importPermitMasterReportForm.MerchantId;
                    Nullable<Guid> method_of_payment_id = importPermitMasterReportForm.MethodOfPaymentId;
                    Nullable<Guid> permit_status_id = importPermitMasterReportForm.PermitStatusId;
                    string currency_type = importPermitMasterReportForm.CurrencyType;
                    string approval_status = importPermitMasterReportForm.ApprovalStatus;
                    List<ImportPermitMasterReport> importPermitMasterReports = new List<ImportPermitMasterReport>();
                    var importPermitMasterReportQuery = db.tblPermits
                        .Where(tp => tp.tblSerialNumberShelf.SerialNumberType.Equals("IMP")
                        && tp.CreatedDate >= importPermitMasterReportForm.dateFrom
                        && tp.CreatedDate <= importPermitMasterReportForm.dateTo);
                    if (importPermitMasterReportForm.MerchantId.HasValue)
                    {
                        importPermitMasterReportQuery = importPermitMasterReportQuery
                            .Where(popmrq => popmrq.MerchantId.Equals(merchant_id.Value));
                    }
                    if (importPermitMasterReportForm.MethodOfPaymentId.HasValue)
                    {
                        importPermitMasterReportQuery = importPermitMasterReportQuery
                            .Where(popmrq => popmrq.MethodOfPaymentId.Value.Equals(method_of_payment_id.Value));
                    }
                    if (importPermitMasterReportForm.PermitStatusId.HasValue)
                    {
                        importPermitMasterReportQuery = importPermitMasterReportQuery
                            .Where(popmrq => popmrq.PermitStatusId.Value.Equals(permit_status_id.Value));
                    }
                    if (!string.IsNullOrEmpty(currency_type))
                    {
                        importPermitMasterReportQuery = importPermitMasterReportQuery
                            .Where(popmrq => popmrq.CurrencyType.Equals(currency_type));
                    }
                    if (!string.IsNullOrEmpty(approval_status))
                    {
                        importPermitMasterReportQuery = importPermitMasterReportQuery
                            .Where(popmrq => popmrq.ApprovalStatus.Equals(approval_status));
                    }
                    importPermitMasterReports = importPermitMasterReportQuery.OrderBy(popmrq => popmrq.Date)
                        .ToList()
                    .Select(popmrq => new ImportPermitMasterReport()
                    {
                        Date = popmrq.Date.Value,
                        NBENumber = popmrq.tblMerchant.NBENumber,
                        PermitNumber = popmrq.PermitNumber,
                        ImporterName = popmrq.tblMerchant.ImporterName,
                        MethodOfPayment = popmrq.tbl_lu_MethodOfPayment.name,
                        PermitStatus = popmrq.tbl_lu_Status.name,
                        CurrencyType = popmrq.CurrencyType,
                        Amount = popmrq.Amount,
                        CurrencyRate = popmrq.CurrencyRate.HasValue ? popmrq.CurrencyRate.Value : 0,
                        AmountInBirr = popmrq.AmountInBirr.HasValue ? popmrq.AmountInBirr.Value : 0,
                        USDRate = popmrq.USDRate.HasValue ? popmrq.USDRate.Value : 0,
                        AmountInUSD = popmrq.AmountInUSD.HasValue ? popmrq.AmountInUSD.Value : 0,
                        RemainingAmount = popmrq.RemainingAmount.HasValue ? popmrq.RemainingAmount.Value : 0,
                        RemainingAmountInUSD = popmrq.RemainingAmountInUSD.HasValue ? popmrq.RemainingAmountInUSD.Value : 0,
                        RemainingAmountInBirr = popmrq.RemainingAmountInBirr.HasValue ? popmrq.RemainingAmountInBirr.Value : 0,
                        LPCONumber = popmrq.LPCONumber,
                        PortOfLoading = string.Join(", ", popmrq.tbl_lu_PortOfLoading.ToList().Select(tlpod => tlpod.name)),
                        PortOfDestination = string.Join(", ", popmrq.tbl_lu_PortOfDestination.ToList().Select(tlpod => tlpod.name)),
                        ShipmentAllowedBy = string.Join(", ", popmrq.tbl_lu_ShipmentAllowedBy.ToList().Select(tlpod => tlpod.name)),
                        Incoterm = string.Join(", ", popmrq.tbl_lu_Incoterm.ToList().Select(tlpod => tlpod.name)),
                        CountryOfOrigin = string.Join(", ", popmrq.tbl_lu_CountryOfOrigin.ToList().Select(tlpod => tlpod.name)),
                        FirstPriorityItems = string.Join(", ", popmrq.tblItemPriorities.Where(tip => tip.Priority.Equals("First Priority"))
                        .ToList().Select(tip => tip.GroupBy + (!string.IsNullOrEmpty(tip.Name) ? ("-" + tip.Name) : ""))),
                        SecondPriorityItems = string.Join(", ", popmrq.tblItemPriorities.Where(tip => tip.Priority.Equals("Second Priority"))
                        .ToList().Select(tip => tip.GroupBy + (!string.IsNullOrEmpty(tip.Name) ? ("-" + tip.Name) : ""))),
                        ThirdPriorityItems = string.Join(", ", popmrq.tblItemPriorities.Where(tip => tip.Priority.Equals("Third Priority"))
                        .ToList().Select(tip => tip.GroupBy + (!string.IsNullOrEmpty(tip.Name) ? ("-" + tip.Name) : ""))),
                        NonPriorityItems = popmrq.NonPriorityItems,
                        ApprovalStatus = popmrq.ApprovalStatus,
                        NBEApprovalRefNumber = popmrq.NBEApprovalRefNumber,
                        QueueRound = popmrq.QueueRound,
                        QueueNumber = popmrq.QueueNumber,
                        OwnSourceValue = popmrq.OwnSourceValue,
                        IncreasedAmount = popmrq.IncreasedAmount.HasValue ? popmrq.IncreasedAmount.Value : 0,
                        IncreasedAmountInUSD = popmrq.IncreasedAmountInUSD.HasValue ? popmrq.IncreasedAmountInUSD.Value : 0,
                        IncreasedAmountInBirr = popmrq.IncreasedAmountInBirr.HasValue ? popmrq.IncreasedAmountInBirr.Value : 0,
                        DecreasedAmount = popmrq.DecreasedAmount.HasValue ? popmrq.DecreasedAmount.Value : 0,
                        DecreasedAmountInUSD = popmrq.DecreasedAmountInUSD.HasValue ? popmrq.DecreasedAmountInUSD.Value : 0,
                        DecreasedAmountInBirr = popmrq.DecreasedAmountInBirr.HasValue ? popmrq.CurrencyRate.Value : 0,
                        PreparedBy = popmrq.USER != null ? (popmrq.USER.Firstname + " " + popmrq.USER.Lastname) : "",
                    }).ToList();
                    if (importPermitMasterReports.Count == 0)
                    {
                        TempData["sErrMsg"] = "There is no data for your filter.";
                    }
                    else
                    {
                        ReportDocument reportDocument = new ReportDocument();
                        reportDocument.Load(Path.Combine(Server.MapPath("~/Reports"), "ImportPermitMasterReport.rpt"));
                        reportDocument.SetDataSource(importPermitMasterReports);
                        string reportDate = importPermitMasterReportForm.dateFrom.ToString("dd/MMM/yyyy")
                            + " to " + importPermitMasterReportForm.dateTo.ToString("dd/MMM/yyyy");
                        reportDocument.SetParameterValue("ReportTitle1", "ZEMEN BANK S.C.");
                        reportDocument.SetParameterValue("ReportTitle2", "Import Permit Master Report ".ToUpper() + reportDate.ToUpper());
                        Response.Buffer = false;
                        Response.ClearContent();
                        Response.ClearHeaders();
                        ExportOptions exportOpts = new ExportOptions();
                        exportOpts.ExportFormatType = ExportFormatType.Excel;
                        ExcelFormatOptions exportFormatOptions = ExportOptions.CreateExcelFormatOptions();
                        exportFormatOptions.ShowGridLines = true;
                        exportFormatOptions.ExcelUseConstantColumnWidth = false;
                        exportOpts.ExportFormatOptions = exportFormatOptions;
                        reportDocument.ExportToHttpResponse(exportOpts, System.Web.HttpContext.Current.Response, false, "Import Permit Master Report");
                        return RedirectToAction("ImportPermitMasterReport");
                    }
                }
            }
            return View(initImportPermitMasterReportForm());
        }

        public InvisiblePaymentMasterReportForm initInvisiblePaymentMasterReportForm()
        {
            InvisiblePaymentMasterReportForm invisiblePaymentMasterReportForm = new InvisiblePaymentMasterReportForm();
            ViewBag.ApplicantId = new SelectList(db.tblApplicants
                .OrderBy(ta => ta.ApplicantName), "Id", "ApplicantName");
            ViewBag.PermitStatusId = new SelectList(db.tbl_lu_Status
                .Where(tls => tls.name.Equals("Active")
                || tls.name.Equals("Cancelled") || tls.name.Equals("Unutilized")),
                "Id", "name");
            List<string> currencyTypeList = db.tblApplications.Select(ta => ta.CurrencyType).Distinct().ToList();
            List<SelectListItem> currencyTypes = new List<SelectListItem>();
            foreach (string currencyType in currencyTypeList)
            {
                SelectListItem selectListItem = new SelectListItem();
                selectListItem.Text = currencyType;
                selectListItem.Value = currencyType;
                currencyTypes.Add(selectListItem);
            }
            ViewBag.CurrencyType = currencyTypes;
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
            return invisiblePaymentMasterReportForm;
        }

        public ActionResult InvisiblePaymentMasterReport()
        {
            return View(initInvisiblePaymentMasterReportForm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult InvisiblePaymentMasterReport(InvisiblePaymentMasterReportForm invisiblePaymentMasterReportForm)
        {
            if (invisiblePaymentMasterReportForm.dateFrom > invisiblePaymentMasterReportForm.dateTo)
            {
                TempData["sErrMsg"] = "Date to should be after date from.";
            }
            else
            {
                if (ModelState.IsValid)
                {
                    Nullable<Guid> applicant_id = invisiblePaymentMasterReportForm.ApplicantId;
                    Nullable<Guid> permit_status_id = invisiblePaymentMasterReportForm.PermitStatusId;
                    string currency_type = invisiblePaymentMasterReportForm.CurrencyType;
                    string approval_status = invisiblePaymentMasterReportForm.ApprovalStatus;
                    string purpose_of_payment = invisiblePaymentMasterReportForm.PurposeOfPayment;
                    List<InvisiblePaymentMasterReport> invisiblePaymentMasterReports = new List<InvisiblePaymentMasterReport>();
                    var invisiblePaymentMasterReportQuery = db.tblApplications
                        .Where(ta => ta.tblSerialNumberShelf.SerialNumberType.Equals("IP")
                        && ta.CreatedDate >= invisiblePaymentMasterReportForm.dateFrom
                        && ta.CreatedDate <= invisiblePaymentMasterReportForm.dateTo);
                    if (invisiblePaymentMasterReportForm.ApplicantId.HasValue)
                    {
                        invisiblePaymentMasterReportQuery = invisiblePaymentMasterReportQuery
                            .Where(popmrq => popmrq.ApplicantId.Equals(applicant_id.Value));
                    }
                    if (invisiblePaymentMasterReportForm.PermitStatusId.HasValue)
                    {
                        invisiblePaymentMasterReportQuery = invisiblePaymentMasterReportQuery
                            .Where(popmrq => popmrq.PermitStatusId.Equals(permit_status_id.Value));
                    }
                    if (!string.IsNullOrEmpty(currency_type))
                    {
                        invisiblePaymentMasterReportQuery = invisiblePaymentMasterReportQuery
                            .Where(popmrq => popmrq.CurrencyType.Equals(currency_type));
                    }
                    if (!string.IsNullOrEmpty(approval_status))
                    {
                        invisiblePaymentMasterReportQuery = invisiblePaymentMasterReportQuery
                            .Where(popmrq => popmrq.ApprovalStatus.Equals(approval_status));
                    }
                    if (!string.IsNullOrEmpty(purpose_of_payment))
                    {
                        invisiblePaymentMasterReportQuery = invisiblePaymentMasterReportQuery
                            .Where(popmrq => popmrq.PurposeOfPayment.Equals(purpose_of_payment));
                    }
                    invisiblePaymentMasterReports = invisiblePaymentMasterReportQuery.OrderBy(popmrq => popmrq.Date)
                        .ToList()
                    .Select(popmrq => new InvisiblePaymentMasterReport()
                    {
                        Date = popmrq.Date,
                        CIFNumber = popmrq.tblApplicant.CIFNumber,
                        PermitNumber = popmrq.PermitNumber,
                        ApplicantName = popmrq.tblApplicant.ApplicantName,
                        PermitStatus = popmrq.tbl_lu_Status.name,
                        CurrencyType = popmrq.CurrencyType,
                        Amount = popmrq.Amount.HasValue ? popmrq.Amount.Value : 0,
                        CurrencyRate = popmrq.CurrencyRate.HasValue ? popmrq.CurrencyRate.Value : 0,
                        AmountInBirr = popmrq.AmountInBirr.HasValue ? popmrq.AmountInBirr.Value : 0,
                        USDRate = popmrq.USDRate.HasValue ? popmrq.USDRate.Value : 0,
                        AmountInUSD = popmrq.AmountInUSD.HasValue ? popmrq.AmountInUSD.Value : 0,
                        RemainingAmount = popmrq.RemainingAmount.HasValue ? popmrq.RemainingAmount.Value : 0,
                        RemainingAmountInUSD = popmrq.RemainingAmountInUSD.HasValue ? popmrq.RemainingAmountInUSD.Value : 0,
                        RemainingAmountInBirr = popmrq.RemainingAmountInBirr.HasValue ? popmrq.RemainingAmountInBirr.Value : 0,
                        ApprovalStatus = popmrq.ApprovalStatus,
                        NBEApprovalRefNumber = popmrq.NBEApprovalRefNumber,
                        QueueRound = popmrq.QueueRound,
                        QueueNumber = popmrq.QueueNumber,
                        OwnSourceValue = popmrq.OwnSourceValue,
                        PurposeOfPayment = popmrq.PurposeOfPayment,
                        Beneficiary = popmrq.Beneficiary,
                        IncreasedAmount = popmrq.IncreasedAmount.HasValue ? popmrq.IncreasedAmount.Value : 0,
                        IncreasedAmountInUSD = popmrq.IncreasedAmountInUSD.HasValue ? popmrq.IncreasedAmountInUSD.Value : 0,
                        IncreasedAmountInBirr = popmrq.IncreasedAmountInBirr.HasValue ? popmrq.IncreasedAmountInBirr.Value : 0,
                        DecreasedAmount = popmrq.DecreasedAmount.HasValue ? popmrq.DecreasedAmount.Value : 0,
                        DecreasedAmountInUSD = popmrq.DecreasedAmountInUSD.HasValue ? popmrq.DecreasedAmountInUSD.Value : 0,
                        DecreasedAmountInBirr = popmrq.DecreasedAmountInBirr.HasValue ? popmrq.CurrencyRate.Value : 0,
                        PreparedBy = popmrq.USER != null ? (popmrq.USER.Firstname + " " + popmrq.USER.Lastname) : "",
                    }).ToList();
                    if (invisiblePaymentMasterReports.Count == 0)
                    {
                        TempData["sErrMsg"] = "There is no data for your filter.";
                    }
                    else
                    {
                        ReportDocument reportDocument = new ReportDocument();
                        reportDocument.Load(Path.Combine(Server.MapPath("~/Reports"), "InvisiblePaymentMasterReport.rpt"));
                        reportDocument.SetDataSource(invisiblePaymentMasterReports);
                        string reportDate = invisiblePaymentMasterReportForm.dateFrom.ToString("dd/MMM/yyyy")
                            + " to " + invisiblePaymentMasterReportForm.dateTo.ToString("dd/MMM/yyyy");
                        reportDocument.SetParameterValue("ReportTitle1", "ZEMEN BANK S.C.");
                        reportDocument.SetParameterValue("ReportTitle2", "Invisible Payment Master Report ".ToUpper() + reportDate.ToUpper());
                        Response.Buffer = false;
                        Response.ClearContent();
                        Response.ClearHeaders();
                        ExportOptions exportOpts = new ExportOptions();
                        exportOpts.ExportFormatType = ExportFormatType.Excel;
                        ExcelFormatOptions exportFormatOptions = ExportOptions.CreateExcelFormatOptions();
                        exportFormatOptions.ShowGridLines = true;
                        exportFormatOptions.ExcelUseConstantColumnWidth = false;
                        exportOpts.ExportFormatOptions = exportFormatOptions;
                        reportDocument.ExportToHttpResponse(exportOpts, System.Web.HttpContext.Current.Response, false, "Invisible Payment Master Report");
                        return RedirectToAction("InvisiblePaymentMasterReport");
                    }
                }
            }
            return View(initInvisiblePaymentMasterReportForm());
        }

    }
}











