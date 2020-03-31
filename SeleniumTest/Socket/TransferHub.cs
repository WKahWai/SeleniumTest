using Microsoft.AspNet.SignalR;
using NLog;
using SeleniumTest.Banks.Core;
using SeleniumTest.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SeleniumTest.Socket
{
    public class TransferHub : Hub
    {
        public static List<SocketItem> ProcessingList = new List<SocketItem>();
        public static List<SocketItem> queue = new List<SocketItem>();
        public Logger logger = LogManager.GetCurrentClassLogger();


        static TransferHub()
        {
            Task.Run(() => Deque());
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            var target = ProcessingList.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);
            if (target != null)
            {
                lock (ProcessingList)
                {
                    BankBase bank = target.Bank;
                    if (bank != null) bank.Dispose();
                }
            }
            else
            {
                lock (queue)
                {
                    target = queue.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);
                    if (target != null) queue.Remove(target);
                }
            }
            return base.OnDisconnected(stopCalled);
        }

        public static void Deque()
        {
            while (true)
            {
                lock (ProcessingList)
                {
                    int MaxQueue = int.Parse(ConfigurationManager.AppSettings["MaxQueue"]);
                    int count = MaxQueue - ProcessingList.Count;
                    if (queue.Count > 0)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (i > (queue.Count - 1)) break;
                            SocketItem item = null;
                            lock (queue)
                            {
                                item = queue[i];
                                item.Bank = BankBase.GetBank(queue[i]);
                                ProcessingList.Add(item);
                                item.Clients.Client(item.ConnectionId).Receive(JsonResponse.success(null, "你的转账正在进行中"));

                            }
                            queue.Remove(queue[i]);
                            Task.Run(() => item.Bank.Start()).ContinueWith(async (task) =>
                            {
                                TransactionResult result = await task;
                                item.Clients.Client(item.ConnectionId).Receive(result.Code != 0 ? JsonResponse.failed("Have error occurred", result) : JsonResponse.success(result, "Request sucessful"));
                                //Thread.Sleep(3000);
                                item.Bank.Dispose();
                                ProcessingList.Remove(item);
                            });
                        }
                    }
                }
                Thread.Sleep(5000);
            }
        }

        public void Begin(string data)
        {
            try
            {
                queue.Add(new SocketItem(Context.ConnectionId, TransferParam.StrToObject(data), Clients));
                Clients.Client(Context.ConnectionId).Receive(JsonResponse.success(null, "你的转账正在排队中请耐心等待哦"));
            }
            catch (Exception ex)
            {
                logger.Error($"Error occur during Hub Start method. {data}. Exception: {ex.Message}");
                Clients.Client(Context.ConnectionId).Receive(JsonResponse.failed("开始初始化列队时出现错误"));
            }
        }

        public void WriteIn(string data)
        {
            lock (ProcessingList)
            {
                var target = ProcessingList.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);
                if (target != null)
                {
                    target.Bank.GetClientResponse = () => data;
                }
            }
        }
    }
}