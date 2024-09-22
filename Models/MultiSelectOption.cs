using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace ZB_FEPMS.Models
{
    public class MultiSelectOption
    {
        public string label { set; get; }
        public string title { set; get; }
        public string value { set; get; }
        public bool selected { set; get; }
    }
}