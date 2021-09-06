## 介绍

江苏电信免费试用接口
2 小时 提速 200MB 下行 50MB 上行

本镜像实现定时自动申请试用，理论上可以持续保持提速状态。敬请试用！！

### 变量

| 变量名 | 默认值 | 介绍                             |
| ------ | ------ | -------------------------------- |
| delay  | 30     | 每隔多少分钟执行一次，要求大于 0 |

### 运行

```
docker run -d \
  --name=js.189.cn-speedup \
  -e delay=30 `#optional` \
  --restart unless-stopped \
  lpyedge/js.189.cn-speedup
```
