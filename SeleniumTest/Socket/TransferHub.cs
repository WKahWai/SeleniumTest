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
        public static List<string> EmergencySkipQueueTerminationList = new List<string>();
        public Logger logger = LogManager.GetCurrentClassLogger();


        static TransferHub()
        {
            Task.Run(() => Deque());
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            Termination();
            return base.OnDisconnected(stopCalled);
        }

        private void Termination()
        {
            var target = ProcessingList.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);
            if (target != null)
            {
                lock (ProcessingList)
                {
                    BankBase bank = target.Bank;
                    if (bank != null) Task.Run(() => bank.Dispose()).ContinueWith(async (c) =>
                    {
                        await c;
                        ProcessingList.Remove(target);
                    });
                }
                Clients.Client(Context.ConnectionId).Receive(JsonResponse.success(null, "正在处理的转账已被强制停止"));
            }
            else
            {
                if (EmergencySkipQueueTerminationList.Count(c => c.Equals(Context.ConnectionId)) == 0 && queue.Count > 0)
                {
                    EmergencySkipQueueTerminationList.Add(Context.ConnectionId);
                    lock (queue)
                    {
                        target = queue.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);
                        if (target != null)
                        {
                            queue.Remove(target);
                            if (target.Bank != null) Task.Run(() => target.Bank.Dispose());
                        }
                        EmergencySkipQueueTerminationList.Remove(Context.ConnectionId);
                    }
                    Clients.Client(Context.ConnectionId).Receive(JsonResponse.success(null, "转账强制停止,已从列队中移除"));
                }
            }
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
                                try { item = queue[i]; } catch { break; }
                                item.Bank = BankBase.GetBank(queue[i]);
                                if (EmergencySkipQueueTerminationList.Where(c => c.Equals(item.ConnectionId)).Count() != 0) break;
                                ProcessingList.Add(item);
                                item.Clients.Client(item.ConnectionId).Receive(JsonResponse.success(null, "你的转账正在进行中"));
                            }
                            queue.Remove(queue[i]);
                            Task.Run(() => item.Bank.Start()).ContinueWith(async (task) =>
                            {
                                TransactionResult result = await task;
                                item.Clients.Client(item.ConnectionId).Receive(result.Code != 0 ? JsonResponse.failed(result.Message ?? "Have error occurred", result, result.Code) : JsonResponse.success(result, result.Message ?? "Request success"));
                                //Thread.Sleep(3000);
                                Task.Run(() => item.Bank.Dispose()).ContinueWith(async (t) =>
                                {
                                    await t;
                                    ProcessingList.Remove(item);
                                });
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

        public void Terminate()
        {
            Termination();
        }
    }
}