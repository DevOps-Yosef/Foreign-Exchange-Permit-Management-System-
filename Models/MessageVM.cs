using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ZB_FEPMS.Models
{
    public class MessageVM
    {
        public MessageVM()
        {

        }

        public string CssClassName { set; get; }
        public string Title { set; get; }
        public string Message { set; get; }
    }
}