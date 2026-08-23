# 更新日志 / Changelog

## 中文

<details>
<summary><strong>0.3.0</strong></summary>

### 恐惧模式

- 新增由主机授权的恐惧模式，每位死亡玩家可以选择自己的怪物模型及对应的空间音效。
- 新增原版风格的 ESC 模型卡片面板，支持运行时模型预览、分页、模型选择、音效控制、本地模型显示与鬼魂语音控制。
- 新增可配置的模型切换、第三人称缩放、空间音效播放、连续音效播放以及附近最大可听玩家数量限制。

### 观战镜头与鬼魂表现

- 新增按 `F3` 观察本地默认鬼魂头部或所选恐惧模型的第三人称模式，并支持鼠标滚轮调节距离。
- 自由视角、原版观战、鬼魂头部与恐惧模型复用本地运动参考系，改善行走、飞船起降、Cruiser 行驶与 Jetpack 飞行时的平滑度。
- 优化兼容玩家间的鬼魂可见性、目标切换与断线恢复、姿态预测、玩家名称修复，并移除屏幕中央的重复观战模型。

### 配置与本地化

- 精简主配置文件，将高级选项迁移至独立的高级配置文件。
- 检测到 LC Chinese Project 时自动使用中文配置与界面，同时保留手动语言选项。

</details>

<details>
<summary><strong>0.2.0</strong></summary>

### 恐惧模式

- 新增再次按键停止及长按切换下一音效的空间怪物音效控制；每位死亡玩家同时只播放一个音效，切换模型时立即停止旧音效。
- 扩充原版 V81 怪物音效目录，覆盖已确认的敌人、行为状态与动画事件字段；抱脸虫从自身的叫声目录开始播放。
- 将食人者拆分为可独立选择的幼体与成体模型。
- 规范纯渲染克隆的 prefab 根位置与旋转，并移除无效的面具人/弹簧头俯仰修正。
- 扩充通用动画与辅助动画中的怪物音效，包含受击、眩晕、激怒、攻击及死亡音效；移除固定的跨玩家播放上限，并新增可选的最近玩家数量限制。
- 保留面具人/弹簧头的根姿态规范，同时恢复其他模型已验证的根姿态；通过完整层级骨骼映射修复食人者成体。
- 新增按住按键并等待当前音效结束后的连续轮播、默认鬼魂玩家死亡音效、模型家族音效隔离、鼠标滚轮第三人称缩放，以及食人者成体的非活动渲染器与网格边界处理。
- 玩家死亡时自动建立默认恐惧选择，无需切换模型即可播放默认死亡音效。
- 新增食人者成体渲染边界校正，使可见模型与同步的鬼魂世界位置对齐。

### 观战镜头

- 优先使用稳定的玩家根节点作为镜头锚点，降低行走和奔跑动画继承到观战视角的位移。
- 无法解析稳定根节点时保留原有动画骨骼回退路径。

### 断线恢复

- 当前观战目标断线且存在其他存活玩家时，自动恢复到有效观战目标。
- 改善非主机客户端的断线目标恢复，避免观战者继续附着在地图外的断线玩家模型。
- 保持模组目标恢复与原版观战切换及增强自由视角输入抑制兼容。

### 远程观战可见性

- 改善兼容客户端死亡玩家在主机与客户端视角中的远程鬼魂头部可见性。
- 稳定目标切换、断线窗口以及增强自由视角和原版视角切换期间的鬼魂头部与名称更新。
- 在创建视觉前拒绝中继回本机的观战姿态，避免在屏幕中央出现重复模型。

### 性能

- 降低观战输入、镜头快照、鬼魂名称文本、运行时分发、网络采样与语音路由玩家查询的常驻查找及分配开销。
- 详细诊断仅在调试配置开启时运行，减少普通游戏中的日志格式化与消息开销。

</details>

<details>
<summary><strong>0.1.3</strong></summary>

### 观战可见性

- 死亡玩家从增强自由视角切换到原版观战时，保持远程鬼魂头部可见。
- 切回增强自由视角后正确恢复姿态同步。
- 增强自由视角关闭但玩家仍在观战时，继续同步原版观战镜头姿态。

### 稳定性

- 新增“增强自由视角 → 原版观战 → 增强自由视角”循环的回归测试。

</details>

<details>
<summary><strong>0.1.2</strong></summary>

### 观战稳定性

- 改善加入未安装模组的主机时的本地观战行为。
- 修复复活后的已连接玩家在其他已安装客户端上仍无法成为观战目标的问题。
- 兼容玩家身份数据不可用时，改善通用 `Player #n` 名称的回退修复。
- 本地会话中通过原版方式选择有效目标后，保持增强自由视角启用。

### 兼容性

- 主机未安装模组时，已安装客户端仍可使用本地自由视角。
- 多人观战状态、鬼魂头部、名称与观战语音路由仍需要兼容的 Enhanced Spectator 客户端及安装模组的中继主机。

</details>

<details>
<summary><strong>0.1.1</strong></summary>

### 视觉

- 默认启用运行时断头观战模型。
- 无法取得运行时头部来源时，保留球体占位模型作为回退。

</details>

<details>
<summary><strong>0.1.0</strong></summary>

首次公开测试版本。

### 观战自由视角

- 为死亡玩家提供本地增强自由视角，并支持配置移动、重定位、重置与速度控制。
- 可在当前原版观战目标周围移动镜头，并限制活动半径。
- 支持配置开关、重定位、重置、快速移动与慢速移动按键。

### 多人观战状态

- 新增兼容客户端能力握手。
- 新增观战目标与观战姿态同步。
- 新增由主机中继的兼容客户端间观战可见性。
- 新增兼容玩家的远程及死亡观战状态显示。
- 新增用于观战名称的玩家身份同步。

### 视觉

- 新增运行时鬼魂头部观战模型。
- 新增鬼魂模型上方的运行时名称。
- 新增运行时断头视觉模式及球体占位回退。
- 新增由语音活动驱动的头部缩放与脉冲。

### 语音

- 新增兼容玩家间可配置的死亡观战语音路由。
- 观战语音根据同步姿态进行空间定位。
- 新增观战语音距离衰减。

### 诊断

- 新增网络、玩家模型、头部来源、语音及视觉生命周期的调试与诊断选项。

</details>

## English

<details>
<summary><strong>0.3.0</strong></summary>

### Fear Mode

- Added a host-authorized fear mode in which each dead player can select their own monster model and matching positional sounds.
- Added the vanilla-styled ESC card panel with runtime model previews, paging, model selection, sound controls, local model visibility, and ghost-voice routing control.
- Added configurable model cycling, third-person zoom, positional sound playback, sequential sound playback, and optional nearby-player sound limits.

### Spectator Camera and Presence

- Added `F3` third-person viewing for the local default ghost head or selected fear model, with mouse-wheel distance adjustment.
- Improved freecam, vanilla spectator, floating-head, and fear-model motion across walking, ship takeoff/landing, Cruiser movement, and Jetpack flight by reusing local moving reference frames.
- Improved compatible-player ghost visibility, target-switch/disconnect recovery, pose prediction, player-name repair, and removal of duplicate center-screen spectator visuals.

### Configuration and Localization

- Simplified the main configuration file by moving advanced options into a separate advanced configuration file.
- Added automatic Chinese configuration and UI selection when LC Chinese Project is detected, with a manual language override.

</details>

<details>
<summary><strong>0.2.0</strong></summary>

### Fear Mode

- Added press-again stop and hold-for-next controls for positional monster sounds, with one active sound per dead player and immediate stop when switching models.
- Expanded the original V81 monster clip catalog from confirmed enemy, behaviour-state, and animation-event fields; Hoarding Bug now starts from its own chitter/screech catalog.
- Split Maneater into selectable baby and adult forms.
- Normalized prefab root position/rotation for renderer-only clones and removed the failed Masked/Spring pitch workaround.
- Expanded assigned monster audio coverage to misc animations and generic animation helpers, including hit, stun, rage, attack, and death clips; removed the fixed cross-player playback cap and added an optional nearest-player limit.
- Kept Masked/Spring root normalization while restoring the previously working root pose for other profiled models, and fixed Maneater Adult with complete-hierarchy bone mapping.
- Added completion-aware held-key fear-sound cycling, Default ghost player-death sounds, model-family clip isolation, mouse-wheel third-person zoom, and a dedicated inactive-renderer/mesh-bounds path for Maneater Adult.
- Established the implicit Default fear selection as soon as a player dies, so Default death sounds work without cycling away and back first.
- Added a post-pose renderer-bounds correction for Maneater Adult to align its visible body with the synchronized ghost world position.

### Spectator Camera

- Reduced spectator-view movement inherited from watched-player walk and run animation by preferring stable player-root camera anchors when available.
- Preserved existing animated-body fallbacks for compatibility when a stable root anchor cannot be resolved.

### Disconnect Recovery

- Added automatic spectator target recovery when the currently watched player disconnects and another living player is available.
- Improved non-host client recovery for disconnected targets so client spectators no longer remain attached to off-map disconnected-player models.
- Kept mod-owned target recovery compatible with vanilla spectator switching and enhanced-freecam input suppression.

### Remote Spectator Visibility

- Improved remote floating-head visibility for dead compatible clients across host and client perspectives.
- Stabilized floating-head and name-tag updates during target changes, disconnect windows, and enhanced-freecam to vanilla-view transitions.
- Rejected relayed local-client spectator echoes before visual creation, preventing a duplicate camera-position head from appearing as a center-screen point.

### Performance

- Reduced steady-state lookup and allocation pressure in spectator input, camera snapshots, floating-head name text, runtime dispatch, network sampling, and voice-routing player lookup paths.
- Kept verbose diagnostics behind debug configuration gates so normal play avoids avoidable log formatting and message churn.

</details>

<details>
<summary><strong>0.1.3</strong></summary>

### Spectator Visibility

- Kept remote floating-head visuals visible when a dead spectator toggles from enhanced freecam to vanilla spectator view.
- Restored enhanced freecam pose sync cleanly after toggling back from vanilla spectator view.
- Continued publishing vanilla spectator camera pose while enhanced freecam is disabled and the player is still spectating.

### Stability

- Added regression coverage for the enhanced-freecam to vanilla-spectator to enhanced-freecam cycle.

</details>

<details>
<summary><strong>0.1.2</strong></summary>

### Spectator Stability

- Improved local-only spectator behavior when joining an unmodded host.
- Repaired cases where revived connected players could remain unavailable as spectator targets on another installed client.
- Improved fallback name repair for generic `Player #n` labels when compatible peer identity data is unavailable.
- Kept enhanced freecam active after valid vanilla spectator target selection in local-only sessions.

### Compatibility

- Local freecam remains available for installed clients when the host is unmodded.
- Multiplayer presence, floating-head visuals, name tags, and routed spectator voice continue to require compatible Enhanced Spectator peers and a modded relay host.

</details>

<details>
<summary><strong>0.1.1</strong></summary>

### Visuals

- Runtime detached-head spectator visuals are now enabled by default.
- Placeholder sphere visuals remain available as the fallback when the runtime head source is unavailable.

</details>

<details>
<summary><strong>0.1.0</strong></summary>

Initial public test release.

### Spectator Freecam

- Client-local enhanced spectator freecam with configurable movement, recenter, reset, and speed controls.
- Camera movement around the current vanilla spectator target with radius limiting.
- Configurable toggle, recenter, reset, fast-move, and slow-move controls.

### Multiplayer Presence

- Modded-peer capability handshake.
- Spectator target sync and spectator pose sync.
- Host-mediated relay for compatible client-to-client spectator visibility.
- Remote and dead spectator visibility for compatible modded peers.
- Peer identity sync for spectator name tags.

### Visuals

- Runtime floating-head spectator visuals.
- Runtime name tags above spectator visuals.
- Runtime detached-head visual mode with placeholder fallback.
- Voice-activity driven head scale and pulse.

### Voice

- Configurable dead-spectator voice routing for compatible peers.
- Positional spectator voice based on synced spectator pose.
- Distance attenuation for routed spectator voice.

### Diagnostics

- Debug and diagnostic controls for networking, player model inspection, head-source inspection, voice diagnostics, and visual lifecycle checks.

</details>
