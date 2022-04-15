## 介绍

江苏电信免费试用接口
2 小时 提速 200MB 下行 50MB 上行

本镜像实现定时自动申请试用，理论上可以持续保持提速状态。敬请试用！！

[![](https://badgen.net/badge/lpyedge/js.189.cn-speedup/blue?icon=docker)](https://hub.docker.com/r/lpyedge/js.189.cn-speedup)
[![](https://badgen.net/docker/pulls/lpyedge/js.189.cn-speedup?icon=docker&label=pulls)](https://hub.docker.com/r/lpyedge/js.189.cn-speedup)
[![](https://badgen.net/docker/stars/lpyedge/js.189.cn-speedup?icon=docker&label=stars)](https://hub.docker.com/r/lpyedge/js.189.cn-speedup)

[![](https://badgen.net/badge/lpyedge/js.189.cn-speedup-docker/purple?icon=github)](https://github.com/lpyedge/js.189.cn-speedup-docker)
[![](https://badgen.net/github/license/lpyedge/js.189.cn-speedup-docker?color=grey)](https://github.com/lpyedge/js.189.cn-speedup-docker/blob/main/LICENSE)


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
