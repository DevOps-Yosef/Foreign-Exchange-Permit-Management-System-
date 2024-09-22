using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace ZB_FEPMS.Helpers
{
    public class ClsInterceptorModule : IHttpModule
    {
        public ClsInterceptorModule()
        { }
        void IHttpModule.Dispose()
        { }

        void IHttpModule.Init(HttpApplication objApplication)
        {
            objApplication.BeginRequest += new EventHandler(this.context_BeginRequest);
        }


        public void context_BeginRequest(object sender, EventArgs e)
        {
        }


    }
}