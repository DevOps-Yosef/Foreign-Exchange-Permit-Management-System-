using System;
using System.ComponentModel.DataAnnotations;

namespace ZB_FEPMS.Models
{
    public class NBEReportForm
    {
        public Nullable<Guid> MethodOfPaymentId { get; set; }
        public string CurrencyType { get; set; }
       
        [Display(Name = "Date from")]
        public DateTime dateFrom { get; set; }
        [Display(Name = "Date to")]
        public DateTime dateTo { get; set; }
    }
}