# CraftimizerCN

基于 [WorkingRobot/Craftimizer](https://github.com/WorkingRobot/Craftimizer) 魔改，针对国服卫月官库的运行环境做出适配、翻译各处的UI文本，并提供以下易用性优化：

* 优化“最佳本地宏”的判断逻辑，提供“自动”权重
* 支持“任务指令2”(宇宙探索等场景)
* 在宏编辑器中保存和另存新宏时，会自动提供默认名称，不再一定需要你手动敲字
* 接入CAC标准，可以将CAC工序码导入到宏编辑器／将本地宏以CAC格式进行分享
* 本地宏界面可以批量导入

<table>
  <tbody>
    <tr>
      <td><img src="https://raw.githubusercontent.com/InfSein/static/master/CraftimizerCN/1.png" /></td>
      <td><img src="https://raw.githubusercontent.com/InfSein/static/master/CraftimizerCN/2.png" /></td>
    </tr>
    <tr>
      <td colspan="2"><img src="https://raw.githubusercontent.com/InfSein/static/master/CraftimizerCN/3.png" /></td>
    </tr>
  </tbody>
</table>

如果这些调整恰好满足了你的需要，你可以考虑迁移到 `CraftimizerCN` 。

> [!CAUTION]\
> 1、`CraftimizerCN` 与 `Craftimizer` 不共用设置，也不提供自动迁移功能。<br>
> 2、切勿同时启用 `CraftimizerCN` 和 `Craftimizer`，这可能导致无法预料的错误。<br>
> 3、虽然开发阶段也一定程度上考虑了在其他客户端运行的兼容性，但不作任何可用性保证。

## 使用方法

将下方的链接添加进你的自定义插件仓库列表，然后就能在插件安装器中下载和安装 `CraftimizerCN` 。

```
https://raw.githubusercontent.com/InfSein/CraftimizerCN/refs/heads/main/manifest.json
```

## 本地开发

```powershell
dotnet restore -r win
dotnet build CraftimizerCN.sln -c Debug
```
