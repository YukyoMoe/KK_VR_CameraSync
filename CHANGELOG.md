# 更新日志

## 0.1.5 - 场景卡切换兜底检测

- 观察 `Studio.sceneInfo` 对象变化，即使某个版本绕过原生加载 Hook，
  仍会安排一次场景卡初始镜头对齐。
- 第一次进入空工作室时只记录初始 `sceneInfo`，不会移动头显。
- `CameraData` 与活动对象相机之间切换时只重建基线，不再把坐标来源
  变化误判为镜头运动。
- 场景加载 Hook 未正常结束时，可在超时后恢复同步并重新安排对齐。
- 配置迁移标记升级为 revision 2。

## 0.1.4 - 使用场景卡保存镜头完成初始对齐

- 在 `LoadScene` / `ImportScene` 开始前暂停同步，在结束后按成功状态恢复，
  并处理嵌套加载与加载失败。
- 初始绝对位置优先读取场景卡的 `cameraSaveData` 或活动对象相机，不再把
  Timeline 自动播放后已经变化的桌面镜头误当成初始位置。
- 如果 Timeline 在对齐前已经自动播放，完成初始对齐后继续应用这段镜头
  增量，避免后续运镜整体带有错误偏移。
- KK_VR 原生 `MoveToCurrent` 后只重建同步基线，不再触发第二次绝对跳转。

## 0.1.3 - 回退启动对齐并修正绝对姿态来源

- 不再进入空工作室时自动对齐，保留 Ermin KK_VR 原生初始视角。
- 只在成功载入/导入场景卡或显式调用 KK_VR 相机重置后对齐。
- 初始绝对位置改为读取工作室最终显示的相机 Transform。
- 默认初始旋转改为 `YawOnly`，避免 Ermin KK_VR 再次直立化 origin 时破坏角度和位置。
- 自动把 v0.1.2 配置中的 `Full` 初始旋转迁移为 `YawOnly`。

## 0.1.2 - 初始相机绝对对齐

- VR 启动及工作室场景载入完成后，把头显绝对位置对齐到当前工作室相机。
- 初始对齐旋转模式与播放跟随旋转模式分离，默认使用 `Full`。
- 初始对齐只在启动、场景载入和 KK_VR 原生相机重置后触发，不会撤销 GripMove。
- 场景加载结束后自动恢复同步，不再等待不存在的自动 `MoveToCurrent` 调用。
- 保留后续逐帧增量同步和 `CutsOnly` 的 2 世界单位阈值行为。

## 0.1.1 - 加载兼容性修复

- 改用 BepInEx 5 自带的 Harmony 2，不再要求不存在的 `0Harmony_BepInEx4.dll`。
- 将 Ermin 工作室 VR 改为软依赖，以兼容由 BepIn4Patcher 转换并从 `BepInEx` 根目录加载的插件。
- 支持直接引用游戏目录中的 `BepInEx\VRGIN_KKCS.dll`。
- 修正推荐安装目录为 `BepInEx\plugins\KK_VR_CameraSync`。

## 0.1.0 - 实验版本

- 新增无需 Timeline 硬依赖的 Studio 相机连续观察。
- 支持 `CameraData` 位置和旋转跟随。
- 支持通过反射读取活动 `OCICamera`。
- 支持 `Full`、`YawOnly` 和 `None` 旋转模式。
- 支持 `AllMotion`、`CutsOnly` 和 `Off` 位置模式。
- 默认保留真实头显的相对追踪姿态。
- KK_VR 执行 `MoveToCurrent()` 后自动重建同步基线。
- `CurrentToCameraCtrl()` 执行期间暂停同步，避免反馈循环。
- 场景加载和导入期间暂停同步，并提供超时恢复。
- 提供中文安装、测试和验证说明。
