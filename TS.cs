using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FluentScheduler;

namespace JSDXTS
{
    public static class TS
    {
        /// <summary>
        ///     执行错误 延迟执行秒数
        /// </summary>
        private const int _errorInterval = 15 * 60;

        /// <summary>
        ///     接近可以提速时间 延迟执行秒数
        /// </summary>
        private const int _speedUpShortInterval = 15;

        /// <summary>
        ///     下次可以提速时间 延迟执行秒数
        ///     提速2小时，这里调整到119分钟18秒
        /// </summary>
        private const int _speedUpLongInterval = (int) (119.3 * 60);

        private static AccountInfo _accountInfo;

        private static readonly Regex RegexUserAccount =
            new(@"<input\s+type=""hidden""\s+id=""HfUserAccount""\s+value=""([-\d]+)""\s*/>",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegexAreaCode =
            new(@"<input\s+type=""hidden""\s+id=""HfAreaCode""\s+value=""([-\d]+)""\s*/>",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegexExpire = new(@"(\d+)点(\d+)分", RegexOptions.Compiled);

        private static HttpWebUtility _httpWebUtility()
        {
            return new HttpWebUtility
            {
                UserAgent =
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 11_5_3) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.107 Safari/537.36 Edg/92.0.902.62",
                
            };
        }

        public static AccountInfo GetAccountInfo()
        {
            using (var wu = _httpWebUtility())
            {
                wu.Accpet = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";
                var htmlStr = wu.ResponseAsync(new Uri("https://ts.js.vnet.cn/speed/index")).Result;
                var matchUserAccount = RegexUserAccount.Match(htmlStr);
                var matchAreaCode = RegexAreaCode.Match(htmlStr);
                if (matchUserAccount.Success && matchUserAccount.Groups[1].Success
                                             && matchAreaCode.Success && matchAreaCode.Groups[1].Success)
                    return new AccountInfo
                    {
                        AreaCode = matchAreaCode.Groups[1].Value,
                        UserAccount = matchUserAccount.Groups[1].Value
                    };
            }
            return null;
        }

        public static string GetAccountStatus(AccountInfo accountInfo)
        {
            using (var wu = _httpWebUtility())
            {
                wu.Accpet =
                    "application/json, text/javascript, */*; q=0.01";

                var data = new Dictionary<string, dynamic>
                {
                    ["action"] = "ExperiencesSpeedModel",
                    ["isPostBk"] = "1",
                    ["UserAccount"] = accountInfo.UserAccount,
                    ["AreaCode"] = accountInfo.AreaCode
                };

                var jsonStr = wu.ResponseAsync(new Uri("https://ts.js.vnet.cn/speed/experiencesSpeedModel"),
                    HttpWebUtility.HttpMethod.POST,
                    data).Result;

                return jsonStr;
            }
        }

        public static string ExecuteSpeedUp(AccountInfo accountInfo)
        {
            using (var wu = _httpWebUtility())
            {
                wu.Accpet =
                    "application/json, text/javascript, */*; q=0.01";

                var data = new Dictionary<string, dynamic>
                {
                    ["action"] = "ExperiencesSpeedBegin",
                    ["isPostBk"] = "1",
                    ["UserAccount"] = accountInfo.UserAccount,
                    ["AreaCode"] = accountInfo.AreaCode
                };

                var jsonStr = wu.ResponseAsync(new Uri("https://ts.js.vnet.cn/speed/beginExperiences"),
                    HttpWebUtility.HttpMethod.POST,
                    data).Result;

                return jsonStr;
            }
        }


        public static void DoTaskLong()
        {
            _accountInfo = GetAccountInfo();
            if (_accountInfo != null)
            {
                var res = GetAccountStatus(_accountInfo);
                if (res.Contains("OK"))
                {
                    res = ExecuteSpeedUp(_accountInfo);
                    if (res.Contains("提速成功"))
                    {
                        LogSuccess(DateTime.Now.AddHours(2));
                        JobManager.AddJob(DoTaskLong, s => s.ToRunOnceIn(_speedUpLongInterval).Seconds());
                    }
                    else
                    {
                        LogError("接口请求错误，稍后重试！");
                        JobManager.AddJob(DoTaskLong, s => s.ToRunOnceIn(_errorInterval).Seconds());
                    }
                }
                else if (RegexExpire.IsMatch(res))
                {
                    var matchExpire = RegexExpire.Match(res);

                    var hour = int.Parse(matchExpire.Groups[1].Value);
                    var minute = int.Parse(matchExpire.Groups[2].Value);
                    //系统返回的过期时间
                    var dateExpire = DateTime.Now.Date.AddHours(hour).AddMinutes(minute);
                    //过期时间小于当前时间超过20小时+ 说明跨天了，日期+1
                    if (DateTime.Now.Subtract(dateExpire).TotalHours > 20)
                        dateExpire = dateExpire.AddDays(1);

                    if (DateTime.Now >= dateExpire)
                    {
                        LogWait();
                        JobManager.AddJob(DoTaskShort,
                            s => s.ToRunOnceIn(_speedUpShortInterval).Seconds()
                        );
                    }
                    else
                    {
                        LogSuccess(dateExpire.AddMinutes(3));
                        //目前系统返回的到期时间不准确，提前了3-4分钟**
                        var delay = (int) dateExpire.AddMinutes(3).Subtract(DateTime.Now).TotalSeconds;
                        JobManager.AddJob(DoTaskLong,
                            s => s.ToRunOnceIn(delay).Seconds()
                        );
                    }
                }
                else
                {
                    LogError("接口请求错误，稍后重试！");
                    JobManager.AddJob(DoTaskLong,
                        s => s.ToRunOnceIn(_errorInterval).Seconds()
                    );
                }
            }
            else
            {
                LogError("接口连接失败，稍后重试！");
                JobManager.AddJob(DoTaskLong,
                    s => s.ToRunOnceIn(_errorInterval).Seconds()
                );
            }
        }

        public static void DoTaskShort()
        {
            if (_accountInfo != null)
            {
                var res = GetAccountStatus(_accountInfo);
                if (res.Contains("OK"))
                {
                    res = ExecuteSpeedUp(_accountInfo);
                    if (res.Contains("提速成功"))
                    {
                        LogSuccess(DateTime.Now.AddHours(2));
                        JobManager.AddJob(DoTaskLong,
                            s => s.ToRunOnceIn(_speedUpLongInterval).Seconds()
                        );
                    }
                    else
                    {
                        LogError("接口请求错误，稍后重试！");
                        JobManager.AddJob(DoTaskLong,
                            s => s.ToRunOnceIn(_errorInterval).Seconds()
                        );
                    }
                }
                else
                {
                    LogWait();
                    JobManager.AddJob(DoTaskShort,
                        s => s.ToRunOnceIn(_speedUpShortInterval).Seconds()
                    );
                }
            }
            else
            {
                LogError("接口连接失败，稍后重试！");
                JobManager.AddJob(DoTaskLong,
                    s => s.ToRunOnceIn(_errorInterval).Seconds()
                );
            }
        }

        private static void LogSuccess(DateTime dateExpire)
        {
            Console.WriteLine(
                $"[{DateTime.Now.ToString("MM-dd HH:mm:ss")}] 成功提速至 200M/50M (下行/上行)\n提速到期时间 {dateExpire.ToString("MM-dd HH:mm")}\n");
        }

        private static void LogWait()
        {
            Console.WriteLine($"[{DateTime.Now.ToString("MM-dd HH:mm:ss")}] 提速重试中");
        }

        private static void LogError(string errorMsg)
        {
            Console.WriteLine($"[{DateTime.Now.ToString("MM-dd HH:mm:ss")}] 提速失败\n错误内容 {errorMsg}\n");
        }


        public class AccountInfo
        {
            public string AreaCode { get; set; }
            public string UserAccount { get; set; }
        }
    }
}