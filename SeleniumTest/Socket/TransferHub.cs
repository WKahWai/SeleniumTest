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
        public static Logger logger = LogManager.GetCurrentClassLogger();


        static TransferHub()
        {
            Task.Run(() => Deque());
        }

        public override Task OnConnected()
        {
            //Clients.Client(Context.ConnectionId).Receive(JsonResponse.success(new List<string>
            //{
            //      "(VND) - 43753453453 - ",
            //      "(VND) - 5686345234324 - ",
            //}, "正在处理的转账已被强制停止", 205));
            return base.OnConnected();
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
                    for (int i = 0; i < count; i++)
                    {
                        if (queue.Count == 0) break;
                        int index = i > (queue.Count - 1) ? i - 1 : i;
                        if (i > (queue.Count - 1)) break;
                        SocketItem item = null;
                        lock (queue)
                        {
                            try { item = queue[index]; } catch { break; }
                            try
                            {
                                item.Bank = BankBase.GetBank(queue[index]);
                                if (EmergencySkipQueueTerminationList.Where(c => c.Equals(item.ConnectionId)).Count() != 0) break;
                                ProcessingList.Add(item);
                                item.Clients.Client(item.ConnectionId).Receive(JsonResponse.success(null, "你的转账正在进行中"));
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"添加连队去处理中的列队异常. Ex - {ex.Message}");
                                item.Clients.Client(item.ConnectionId).Receive(JsonResponse.failed(message: "无法转账系统检测到参数加密异常"));
                            }
                        }
                        queue.Remove(queue[index]);
                        if (item != null && item?.Bank != null)
                        {
                            Task.Run(() => item.Bank.Start()).ContinueWith(async (response) =>
                            {
                                item.Clients.Client(item.ConnectionId).Receive(await response);
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
                var param = TransferParam.StrToObject(data);
                if (string.IsNullOrEmpty(param.payload)) throw new Exception("Payload is null");
                if (string.IsNullOrEmpty(param.FromBank)) throw new Exception("From Bank is null, frontend have error");
                if (string.IsNullOrEmpty(param.TargetBank)) throw new Exception("Target bank is null frontend have error");
                if (string.IsNullOrEmpty(param.RecipientAccount)) throw new Exception("Company account is null");
                if (string.IsNullOrEmpty(param.AccountID)) throw new Exception("User account is null");
                if (string.IsNullOrEmpty(param.Password)) throw new Exception("Password is null");
                if (param.Amount == 0) throw new Exception("Invalid amount");
                if (param.OTPType <= 0 || param.OTPType > 2) throw new Exception("Invalid OTP type");
                param.payload = HttpUtility.UrlDecode(param.payload);
                queue.Add(new SocketItem(Context.ConnectionId, param, Clients));
                Clients.Client(Context.ConnectionId).Receive(JsonResponse.success(null, "你的转账正在排队中请耐心等待哦"));
            }
            catch (Exception ex)
            {
                logger.Error($"Error occur during Hub Start method. {data}. Exception: {ex.Message}");
                Clients.Client(Context.ConnectionId).Receive(JsonResponse.failed("开始初始化列队时出现错误", code: 402));
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