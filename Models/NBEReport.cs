using System;

namespace ZB_FEPMS.Models
{
    public class NBEReport
    {
        public DateTime Date { get; set; }
        public string NBENumber { get; set; }
        public string PermitNumber { get; set; }
        public string ImporterName { get; set; }
        public string MethodOfPayment { get; set; }
        public string CurrencyType { get; set; }
        public decimal Amount { get; set; }
        public decimal CurrencyRate { get; set; }
        public decimal AmountInBirr { get; set; }
        public string LPCONumber { get; set; }
    }
}




