using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(ZB_FEPMS.Hubs.Startup))]

namespace ZB_FEPMS.Hubs
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Any connection or hub wire up and configuration should go here
            //app.MapSignalR();
        }
    }
}
