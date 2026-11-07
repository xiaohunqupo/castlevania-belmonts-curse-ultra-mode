# 维护文档

面向参与开发和维护本 mod 的人。玩家使用说明见仓库根目录的 [README](../README.zh-CN.md)。

| 文档 | 内容 |
| --- | --- |
| [开发环境与调试](development.md) | 环境准备、构建、日志与 F11 导出、查找游戏类型、IL2CPP 注意事项 |
| [架构](architecture.md) | 加载流程、功能模块机制、动作追踪、设置、错误隔离 |
| [功能模块参考](features.md) | 每个模块挂在哪些游戏方法上、改了哪些数据、有哪些坑 |
| [新增一个功能](adding-a-feature.md) | 从配置项到设置页、README 的完整步骤 |
| [设置页与 Belmont UI](settings-page.md) | 三种设置页的选择逻辑、Minimap 契约、内嵌 Belmont UI 的同步 |
| [测试清单](testing.md) | 改动后需要在游戏里手动验证的内容 |
| [发布流程](release.md) | 改版本号、打包、打标签、发 GitHub Release |

贡献方式和提交规范见 [CONTRIBUTING](../CONTRIBUTING.md)。

## 一句话概览

BepInEx 6 IL2CPP 插件。`src/Plugin.cs` 启动时，把每个带 `[Feature("名字")]` 的类作为一个独立模块安装 Harmony 补丁并调用它的 `Init()`；大部分模块不直接打补丁，而是订阅 `ActionTracker` 在主角每次状态更新后发出的事件，读写游戏数据。任何模块出错都只会让它自己退回原版。
