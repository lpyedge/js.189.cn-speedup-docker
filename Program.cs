using System;
using System.Threading.Tasks;
using FluentScheduler;
using Microsoft.Extensions.Hosting;

namespace JSDXTS
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            JobManager.Initialize();
            
            JobManager.AddJob(TS.DoTaskLong,
                s => s.ToRunNow()
            );
            
            Console.WriteLine($"js.189.cn-speedup(ver {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()})");
            
            Host.CreateDefaultBuilder()
                .Build().Run();
        }
    }
}