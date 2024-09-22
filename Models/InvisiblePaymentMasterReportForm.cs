using System;
using System.ComponentModel.DataAnnotations;

namespace ZB_FEPMS.Models
{
    public class InvisiblePaymentMasterReportForm
    {
        public Nullable<Guid> ApplicantId { get; set; }
        public Nullable<Guid> PermitStatusId { get; set; }
        public string CurrencyType { get; set; }
        public string ApprovalStatus { get; set; }
        public string PurposeOfPayment { get; set; }
        [Display(Name = "Date from")]
        public DateTime dateFrom { get; set; }
        [Display(Name = "Date to")]
        public DateTime dateTo { get; set; }
    }
}