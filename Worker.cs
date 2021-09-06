using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JSDXTS
{
    //base uri : https://ts.js.vnet.cn/speed/index
    public class Worker : BackgroundService
    {
        /// <summary>
        /// 每半小时执行一次
        /// </summary>
        private static int delay = 1800000;
   
        private readonly ILogger<Worker> _logger;
        
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            
            //环境变量取delay字段（延迟请求xx分钟）
            var delayStr = Environment.GetEnvironmentVariable("delay",
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? EnvironmentVariableTarget.Machine
                    : EnvironmentVariableTarget.Process);
            
            //环境变量delay字段存在且可以转化为大于0的int值 
            if (int.TryParse(delayStr, out int delayTemp) && delayTemp > 0)
            {
                //延迟时间切换为系统内部使用的毫秒数
                delay = delayTemp * 60000;
            }
            
            
            //环境变量取TZ字段（当前时区）
            var tzStr = Environment.GetEnvironmentVariable("TZ",
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? EnvironmentVariableTarget.Machine
                    : EnvironmentVariableTarget.Process);
            
            
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                //_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                ts();
                
                await Task.Delay(delay, stoppingToken);
            }
        }

        private Regex RegexUserAccount =
            new Regex(@"<input\s+type=""hidden""\s+id=""HfUserAccount""\s+value=""([-\d]+)""\s*/>",RegexOptions.Compiled|RegexOptions.IgnoreCase);
        private Regex RegexAreaCode =
            new Regex(@"<input\s+type=""hidden""\s+id=""HfAreaCode""\s+value=""([-\d]+)""\s*/>",RegexOptions.Compiled|RegexOptions.IgnoreCase);

        private void ts()
        {
            Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] js.189.cn-speedup 执行提速操作 ");
            try
            {
                HttpWebUtility wu = new HttpWebUtility();
                var htmlStr= wu.Response(new Uri("https://ts.js.vnet.cn/speed/index"));
                var matchUserAccount= RegexUserAccount.Match(htmlStr);
                var matchAreaCode= RegexAreaCode.Match(htmlStr);
                if (matchUserAccount.Success && matchUserAccount.Groups[1].Success
                && matchAreaCode.Success && matchAreaCode.Groups[1].Success)
                {
                    Console.WriteLine($"地区：{matchAreaCode.Groups[1].Value}");
                    Console.WriteLine($"账号：{matchUserAccount.Groups[1].Value}");

                    wu.Accpet = "application/json; charset=UTF-8";
                    //wu.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                    
                    var data0 = new Dictionary<string, string>()
                    {
                        ["action"]="ExperiencesSpeedModel",
                        ["isPostBk"]="1",
                        ["UserAccount"]=matchUserAccount.Groups[1].Value,
                        ["AreaCode"]=matchAreaCode.Groups[1].Value
                    };
                    var jsonStr0 = wu.Response(new Uri("https://ts.js.vnet.cn/speed/experiencesSpeedModel"),
                        HttpWebUtility.HttpMethod.Post,
                        data0,null,Encoding.UTF8);
                    
                    if (jsonStr0.Contains("OK"))
                    {
                        var data1 = new Dictionary<string, string>()
                        {
                            ["action"]="ExperiencesSpeedBegin",
                            ["isPostBk"]="1",
                            ["UserAccount"]=matchUserAccount.Groups[1].Value,
                            ["AreaCode"]=matchAreaCode.Groups[1].Value
                        };
                        var jsonStr1 = wu.Response(new Uri("https://ts.js.vnet.cn/speed/beginExperiences"),
                            HttpWebUtility.HttpMethod.Post,
                            data1,null,Encoding.UTF8);
                        
                        Console.WriteLine(jsonStr1);
                    }
                    else
                    {
                        Console.WriteLine(jsonStr0);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            Console.WriteLine("\n");
        }
    }
}