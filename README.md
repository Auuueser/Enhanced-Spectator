<h1 align="center">Enhanced Spectator</h1>

<p align="center">
  <strong>死亡不是离场，只是换了一个视角。</strong><br>
  为 <strong>Lethal Company V81</strong> 打造的沉浸式死亡观战体验
</p>

<p align="center">
  <a href="https://github.com/Auuueser/Enhanced-Spectator/releases"><img alt="版本 0.3.1" src="https://img.shields.io/badge/版本-0.3.1-E35B18?style=flat-square&labelColor=24282C"></a>
  <a href="https://store.steampowered.com/app/1966720/Lethal_Company/"><img alt="游戏版本 V81" src="https://img.shields.io/badge/游戏-V81-E35B18?style=flat-square&labelColor=24282C"></a>
  <a href="https://github.com/BepInEx/BepInEx"><img alt="BepInEx 5" src="https://img.shields.io/badge/运行环境-BepInEx%205-526D82?style=flat-square&labelColor=24282C"></a>
  <a href="https://github.com/Auuueser/Enhanced-Spectator/blob/main/LICENSE"><img alt="GPL-3.0" src="https://img.shields.io/badge/许可-GPL--3.0-E35B18?style=flat-square&labelColor=24282C"></a>
</p>

<p align="center">
  <a href="https://github.com/Auuueser/Enhanced-Spectator/releases"><img alt="下载版本" src="https://img.shields.io/badge/下载-版本-E35B18?style=flat-square&labelColor=24282C"></a>
  <a href="https://github.com/Auuueser/Enhanced-Spectator/issues"><img alt="问题反馈" src="https://img.shields.io/badge/问题-反馈-C84C2F?style=flat-square&labelColor=24282C"></a>
  <a href="https://github.com/Auuueser/Enhanced-Spectator/blob/v0.3.1/CHANGELOG.md"><img alt="更新日志" src="https://img.shields.io/badge/版本-更新日志-526D82?style=flat-square&labelColor=24282C"></a>
</p>

## 观战，也可以很有存在感

| 自由观战 | 鬼魂视角 | 恐惧模式 | 空间语音 |
|:--|:--|:--|:--|
| 平滑自由镜头、目标重定位与断线恢复 | 第一/第三人称切换，滚轮调节距离 | 死亡玩家自选怪物模型与对应音效 | 鬼魂语音、位置同步与距离衰减 |

## 恐惧模式面板

<table>
  <tr>
    <td width="50%" align="center">
      <img src="https://raw.githubusercontent.com/Auuueser/Enhanced-Spectator/v0.3.1/assets/readme/fear-mode-entry-zh.png" alt="中文 ESC 菜单右上角恐惧模式入口图标"><br>
      <sub>① 打开 ESC 菜单，点击右上角鬼魂图标</sub>
    </td>
    <td width="50%" align="center">
      <img src="https://raw.githubusercontent.com/Auuueser/Enhanced-Spectator/v0.3.1/assets/readme/fear-mode-panel-zh.png" alt="中文恐惧模式模型卡片窗口"><br>
      <sub>② 选择模型、播放音效并控制鬼魂语音</sub>
    </td>
  </tr>
</table>

- **主机授权**：仅安装 Enhanced Spectator 的房主可开启本局的恐惧模式。
- **玩家自选**：每位死亡玩家可以选择自己的怪物模型及对应音效。
- **模型显示**：每位死亡玩家可独立选择显示怪物模型。

## 核心体验

- **第三人称**：死亡后按 `F3` 开启第三人称下的默认鬼魂头部或恐惧模型，滚轮实时缩放。
- **怪物音效**：播放当前模型的原版音效，音效跟随鬼魂的真实世界位置。
- **兼容玩家可见性**：同步鬼魂姿态、名称、说话状态与路由语音；无兼容主机时安全退回本地能力。

## 默认操作

| 操作 | 默认按键 |
|:--|:--|
| 开关自由观战 / 返回原版视角 | `F6` / `F7` |
| 移动 / 上升 / 下降 | `W A S D` / `Space` / `Left Ctrl` |
| 加速 / 慢速 / 重置位置 | `Left Shift` / `Left Alt` / `R` |
| 切换第三人称 / 调整距离 | `F3` / 鼠标滚轮 |
| 播放、停止或长按连续播放恐惧音效 | `Z` |
| 下一音效并播放 | `X` |
| 上一个 / 下一个怪物模型 | `←` / `→` |

## 安装与配置

推荐通过 **r2modman / Thunderstore Mod Manager** 安装发布版本，依赖会由管理器自动处理。

<details>
<summary><strong>手动安装</strong></summary>

1. 安装 `BepInExPack 5.4.2100`。
2. 从 [Releases](https://github.com/Auuueser/Enhanced-Spectator/releases) 下载对应版本。
3. 将 `EnhancedSpectator.dll` 放入 `BepInEx/plugins/EnhancedSpectator/`。
4. 启动一次游戏以生成配置。

</details>

常用设置与高级调试项分离：

```text
BepInEx/config/Auuueser.EnhancedSpectator.cfg
BepInEx/config/Auuueser.EnhancedSpectator.Advanced.cfg
```

检测到 **LC Chinese Project** 时，配置与界面会自动使用中文。也可以通过 `General.ConfigLanguage` 强制选择中文或英文。

其他镜头、语音或观战模组若同时写入相同系统，可能产生冲突。

<details>
<summary><strong>本地构建</strong></summary>

需要 .NET SDK 8 与本地 Lethal Company 安装。游戏程序集仅从本地目录引用，不包含在仓库或发布包中。

```powershell
dotnet restore EnhancedSpectator.sln --source https://nuget.bepinex.dev/v3/index.json --source https://api.nuget.org/v3/index.json
dotnet build src/EnhancedSpectator/EnhancedSpectator.csproj -c Release -p:GameDir="D:\Steam\steamapps\common\Lethal Company"
```

项目不使用反射访问游戏成员；所有游戏接口均通过 `GameInterop` 隔离。

</details>

## 反馈与许可

反馈问题时，请在 [GitHub Issues](https://github.com/Auuueser/Enhanced-Spectator/issues) 附上复现步骤、模组列表、截图及 `BepInEx/LogOutput.log`。

项目采用 [GNU General Public License v3.0](https://github.com/Auuueser/Enhanced-Spectator/blob/main/LICENSE)。

---

<details>
<summary><strong>English</strong></summary>

<br>

<table>
  <tr>
    <td width="50%" align="center">
      <img src="https://raw.githubusercontent.com/Auuueser/Enhanced-Spectator/v0.3.1/assets/readme/fear-mode-entry-en.png" alt="English fear-mode entry icon in the top-right corner of the ESC menu"><br>
      <sub>① Open the ESC menu and select the ghost icon</sub>
    </td>
    <td width="50%" align="center">
      <img src="https://raw.githubusercontent.com/Auuueser/Enhanced-Spectator/v0.3.1/assets/readme/fear-mode-panel-en.png" alt="English fear-mode model card window"><br>
      <sub>② Select a model, play sounds, and control ghost voice</sub>
    </td>
  </tr>
</table>

### Highlights

| Freecam | Ghost View | Fear Mode | Spatial Voice |
|:--|:--|:--|:--|
| Smooth movement, recentering, and target recovery | First/third-person self-ghost view with adjustable distance | Player-selected monster models and matching sounds | Routed ghost voice with synchronized position and attenuation |

- **Third person:** Press `F3` after death to enable a third-person view of your default ghost head or fear model; use the mouse wheel to zoom.
- **Monster sounds:** Play the selected model's original sounds from the ghost's real-world position.
- **Compatible-player visibility:** Synchronizes ghost pose, name, speaking state, and routed voice, with a safe local fallback when no compatible host is available.
### Fear mode

- **Host authorization:** Only a host with Enhanced Spectator installed can enable fear mode for the session.
- **Player choice:** Each dead player can select their own monster model and matching sounds.
- **Model display:** Each dead player can independently choose whether to display monster models.

### Default controls

| Action | Default |
|:--|:--|
| Toggle freecam / return to vanilla view | `F6` / `F7` |
| Move / ascend / descend | `W A S D` / `Space` / `Left Ctrl` |
| Fast / slow / recenter | `Left Shift` / `Left Alt` / `R` |
| Toggle third person / adjust distance | `F3` / mouse wheel |
| Play, stop, or hold for sequential fear sounds | `Z` |
| Next sound and play | `X` |
| Previous / next fear model | `←` / `→` |

### Installation and configuration

Install with **r2modman / Thunderstore Mod Manager**, or place `EnhancedSpectator.dll` in `BepInEx/plugins/EnhancedSpectator/`.

```text
BepInEx/config/Auuueser.EnhancedSpectator.cfg
BepInEx/config/Auuueser.EnhancedSpectator.Advanced.cfg
```

When **LC Chinese Project** is detected, Enhanced Spectator automatically uses Chinese configuration and UI text. `General.ConfigLanguage` can override this behavior.

### Feedback and license

Report issues through [GitHub Issues](https://github.com/Auuueser/Enhanced-Spectator/issues) with reproduction steps, your mod list, screenshots, and `BepInEx/LogOutput.log`.

Licensed under the [GNU General Public License v3.0](https://github.com/Auuueser/Enhanced-Spectator/blob/main/LICENSE).

</details>
