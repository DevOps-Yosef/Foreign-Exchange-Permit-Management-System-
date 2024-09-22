using System;

namespace ZB_FEPMS.Models
{
    public class PurchaseOrderPermitMasterReport
    {
        public DateTime Date { get; set; }
        public string NBENumber { get; set; }
        public string PermitNumber { get; set; }
        public string ImporterName { get; set; }
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
        public string LPCONumber { get; set; }
        public string PortOfLoading { get; set; }
        public string PortOfDestination { get; set; }
        public string ShipmentAllowedBy { get; set; }
        public string Incoterm { get; set; }
        public string CountryOfOrigin { get; set; }
        public string FirstPriorityItems { get; set; }
        public string SecondPriorityItems { get; set; }
        public string ThirdPriorityItems { get; set; }
        public string NonPriorityItems { get; set; }
        public string ApprovalStatus { get; set; }
        public string NBEApprovalRefNumber { get; set; }
        public string QueueRound { get; set; }
        public string QueueNumber { get; set; }
        public string OwnSourceValue { get; set; }
        public decimal IncreasedAmount { get; set; }
        public decimal IncreasedAmountInUSD { get; set; }
        public decimal IncreasedAmountInBirr { get; set; }
        public decimal DecreasedAmount { get; set; }
        public decimal DecreasedAmountInUSD { get; set; }
        public decimal DecreasedAmountInBirr { get; set; }
        public string PreparedBy { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}




