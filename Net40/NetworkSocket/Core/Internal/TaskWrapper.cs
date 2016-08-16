﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace NetworkSocket.Core
{
    /// <summary>
    /// Task包装
    /// </summary>
    internal class TaskWrapper
    {
        /// <summary>
        /// 安全字典
        /// </summary>
        private readonly static ConcurrentDictionary<Type, Func<Task, object>> dic = new ConcurrentDictionary<Type, Func<Task, object>>();

        /// <summary>
        /// 创建Task类型获取Result的委托
        /// </summary>
        /// <param name="taskType">Task实例的类型</param>
        /// <returns></returns>
        private static Func<Task, object> CreateTaskResultInvoker(Type taskType)
        {
            if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // lambda = task => (object)(((Task<T>)task).Result)
                var arg = Expression.Parameter(typeof(Task));
                var castArg = Expression.Convert(arg, taskType);
                var fieldAccess = Expression.Property(castArg, "Result");
                var castResult = Expression.Convert(fieldAccess, typeof(object));
                var lambda = Expression.Lambda<Func<Task, object>>(castResult, arg);
                return lambda.Compile();
            }

            return task => null;
        }

        /// <summary>
        /// 从值中获得包装器
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="valueType">值的类型</param>
        /// <returns></returns>
        public static TaskWrapper Parse(object value, Type valueType)
        {
            return new TaskWrapper(value, valueType);
        }

        /// <summary>
        /// 值
        /// </summary>
        private readonly object value;
        /// <summary>
        /// 值的类型
        /// </summary>
        private readonly Type valueType;

        /// <summary>
        /// 值转换为task
        /// </summary>
        private readonly Task task;

        /// <summary>
        /// Task包装
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="valueType">值的类型</param>
        private TaskWrapper(object value, Type valueType)
        {
            this.value = value;
            this.valueType = valueType;
            this.task = value as Task;
        }

        /// <summary>
        /// 获取结果
        /// </summary>
        /// <returns></returns>
        public object GetResult()
        {
            if (this.task == null)
            {
                return this.value;
            }
            else
            {
                var invoker = TaskWrapper.dic.GetOrAdd(this.valueType, (type) => TaskWrapper.CreateTaskResultInvoker(type));
                return invoker.Invoke(this.task);
            }
        }

        /// <summary>
        /// 完成时继续延续任务
        /// </summary>
        /// <param name="action">行为</param>
        /// <exception cref="ArgumentNullException"></exception>
        public TaskWrapper ContinueWith(Action<TaskWrapper> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException();
            }
            if (this.task == null)
            {
                action(this);
            }
            else
            {
                this.task.ContinueWith(t => action(this));
            }
            return this;
        }

        /// <summary>
        /// 取消包装
        /// </summary>
        /// <returns></returns>
        public Task UnWrap()
        {
            if (this.task == null)
            {
                return new Task<object>(() => this.value);
            }
            else
            {
                return this.task;
            }
        }
    }
}
