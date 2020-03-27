using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using SeleniumTest.Socket;

[assembly: OwinStartup(typeof(SeleniumTest.Startup))]

namespace SeleniumTest
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapAzureSignalR(typeof(TransferHub).FullName);
        }
    }
}
