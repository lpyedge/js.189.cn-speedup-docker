using System;
using System.Threading.Tasks;
using FluentScheduler;
using Microsoft.Extensions.Hosting;

namespace JSDXTS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            JobManager.Initialize();
            
            JobManager.AddJob(TS.DoTaskLong,
                s => s.ToRunOnceIn(15).Seconds()
            );
            
            //任务不执行/自动终端？添加异常事件监测错误
            JobManager.JobException += (args) =>
            {
                Console.WriteLine( $"JobError: [{args.Name}] {args.Exception.Message}");
            };
            
            Console.WriteLine($"js.189.cn-speedup(ver {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()})");
            
            Host.CreateDefaultBuilder()
                .Build().Run();
        }
    }
}