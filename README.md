## 介绍

江苏电信免费试用接口
2 小时 提速 200MB 下行 50MB 上行

本镜像实现定时自动申请试用，理论上可以持续保持提速状态。敬请试用！！


Docker Hub 地址
[https://hub.docker.com/r/lpyedge/js.189.cn-speedup](https://hub.docker.com/r/lpyedge/js.189.cn-speedup)


### 变量

| 变量名 | 默认值 | 介绍                                  |
| ------ | ------ |-------------------------------------|
| ~~interval~~  | ~~15~~     | ~~每隔多少分钟执行一次~~  （已弃用）自适应间隔时长|

### 运行

```
docker run -d \
  --name=js.189.cn-speedup \
  --restart unless-stopped \
  lpyedge/js.189.cn-speedup
```
