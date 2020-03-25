using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BankAPI.Model
{
    public class StepLoopOption
    {
        public int MaxLoop { get; set; }

        /// <summary>
        /// Time of thread sleep in second
        /// </summary>
        public int SleepInterval
        {
            get
            {
                return _Interval;
            }
            set
            {
                _Interval = value * 1000;
            }
        }
        private int _Interval;
        public Func<Action, bool> ActionTask { get; }

        public StepLoopOption(Func<Action, bool> ActionTask)
        {
            this.ActionTask = ActionTask;
        }

        public static StepLoopOption DefaultOption = new StepLoopOption((i) => true);
    }

    public class StepLoopResult
    {
        public bool IsComplete { get; set; }
        public bool HasError { get; set; }
        public string Message { get; set; }

        public static StepLoopResult SetTimeout()
        {
            return new StepLoopResult
            {
                IsComplete = false,
                HasError = true,
                Message = "执行中的任务超时，系统强制终止任务"
            };
        }

        public static StepLoopResult Error(string message)
        {
            return new StepLoopResult
            {
                IsComplete = false,
                HasError = true,
                Message = message
            };
        }

        public static StepLoopResult Complete()
        {
            return new StepLoopResult
            {
                IsComplete = true,
                HasError = false,
                Message = "任务完成"
            };
        }
    }
}