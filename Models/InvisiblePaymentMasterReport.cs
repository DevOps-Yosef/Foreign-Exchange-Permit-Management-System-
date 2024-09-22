using System;

namespace ZB_FEPMS.Models
{
    public class InvisiblePaymentMasterReport
    {
        public DateTime Date { get; set; }
        public string CIFNumber { get; set; }
        public string PermitNumber { get; set; }
        public string ApplicantName { get; set; }
        public string PermitStatus { get; set; }
        public string CurrencyType { get; set; }
        public decimal Amount { get; set; }
        public decimal CurrencyRate { get; set; }
        public decimal AmountInBirr { get; set; }
        public decimal USDRate { get; set; }
        public decimal AmountInUSD { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal RemainingAmountInUSD { get; set; }
        public decimal RemainingAmountInBirr { get; set; }
        public string ApprovalStatus { get; set; }
        public string NBEApprovalRefNumber { get; set; }
        public string QueueRound { get; set; }
        public string QueueNumber { get; set; }
        public string OwnSourceValue { get; set; }
        public string PurposeOfPayment { get; set; }
        public string Beneficiary { get; set; }
        public decimal IncreasedAmount { get; set; }
        public decimal IncreasedAmountInUSD { get; set; }
        public decimal IncreasedAmountInBirr { get; set; }
        public decimal DecreasedAmount { get; set; }
        public decimal DecreasedAmountInUSD { get; set; }
        public decimal DecreasedAmountInBirr { get; set; }
        public string PreparedBy { get; set; }
    }
}




