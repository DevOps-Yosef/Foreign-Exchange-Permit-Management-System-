using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace ZB_FEPMS.Models
{
    public class PermitDetails
    {
        public List<string> PortOfLoadingIds { set; get; }
        public List<string> PortOfDestinationIds { set; get; }
        public List<string> ShipmentAllowedByIds { set; get; }
        public List<string> IncotermIds { set; get; }
        public string CurrencyType { set; get; }
        public string ApprovalStatus { set; get; }
        public string NBEApprovalRefNumber { set; get; }
        public string OwnSourceValue { set; get; }
        public string QueueRound { set; get; }
        public string QueueNumber { set; get; }
        public decimal CurrencyRate { set; get; }
        public decimal AmountInBirr { set; get; }
        public decimal USDRate { set; get; }
        public decimal AmountInUSD { set; get; }
        public List<string> CountryOfOriginIds { set; get; }
        public List<MultiSelectOption> FirstPriorityTopLevels { set; get; }
        public List<SelectListItem> FirstPrioritySubLevels { set; get; }
        public List<MultiSelectOption> SecondPriorityTopLevels { set; get; }
        public List<SelectListItem> SecondPrioritySubLevels { set; get; }
        public List<MultiSelectOption> ThirdPriorityTopLevels { set; get; }
        public List<SelectListItem> ThirdPrioritySubLevels { set; get; }
        public string NonPriorityItems { set; get; }
        public List<string> PermitNumbers { set; get; }

    }
}