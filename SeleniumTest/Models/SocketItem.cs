using Microsoft.AspNet.SignalR.Hubs;
using SeleniumTest.Banks.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SeleniumTest.Models
{
    public class SocketItem
    {
        public string ConnectionId { get; }

        public IHubCallerConnectionContext<dynamic> Clients { get; }

        public TransferParam param { get; }

        public BankBase Bank { get; set; }

        private DateTime InitTime = DateTime.Now;

        public TimeSpan FinisedTime
        {
            get
            {
                return DateTime.Now.Subtract(InitTime);
            }
        }

        public SocketItem(string ConnectionId, TransferParam param, IHubCallerConnectionContext<dynamic> Clients)
        {
            this.ConnectionId = ConnectionId;
            this.param = param;
            this.Clients = Clients;
        }
    }
}