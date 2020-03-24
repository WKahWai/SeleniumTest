﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BankAPI.Model
{
    public class StepLoopOption
    {
        public int MaxLoop { get; set; }

        public int SleepInterval { get; set; }

        public Func<bool> ActionTask { get; }

        public StepLoopOption(Func<bool> ActionTask)
        {
            this.ActionTask = ActionTask;
            MaxLoop = 10;
            SleepInterval = 1 * 1000;
        }

        public static StepLoopOption DefaultOption = new StepLoopOption(() => true);
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