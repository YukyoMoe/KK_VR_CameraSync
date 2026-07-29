# 验证说明

## 审查基准

本实现针对以下源码版本进行设计：

- `Ermin610/KK_VR` main：
  `f196573b7c83de96939ee5e0321df9e38daf8747`
- `YukyoMoe/KKS_VR_TimelineCameraSync` main：
  `7a79fa5bb01408a637e1c46d8939bd0dadff3278`

如果目标游戏安装中的 DLL 来自其他提交或经过二次修改，需要重新核对
类型名、方法签名和相机坐标计算。

## 已完成的静态和构建验证

- 能够编译为 .NET Framework 3.5 兼容程序集。
- CLR image runtime 为 `v2.0.50727`。
- 程序集名称为 `KK_VR_CameraSync`。
- 程序集版本为 `0.1.5.0`。
- 没有 Timeline 程序集引用或 Timeline 硬依赖。
- KK_VR 软依赖 GUID 与审查到的插件源码一致。
- 已实现连续 `CameraData` 位姿增量同步。
- 活动对象相机读取为可选的反射路径。
- KK_VR 原生 `MoveToCurrent` 执行后只会重建相机基线，不会再次执行
  绝对位置对齐。
- `CurrentToCameraCtrl` 执行期间会暂停同步，避免反向写回触发反馈。
- 仅场景卡载入/导入结束或 `sceneInfo` 确认发生场景卡切换后执行一次
  绝对对齐。
- 空工作室启动及 Unity 子场景载入不会触发绝对对齐。
- 初始绝对位置优先读取场景卡保存的 `cameraSaveData` 或活动对象相机；
  增量同步仍观察最终工作室相机数据。
- Timeline 自动播放已经前进时，会在初始对齐后衔接保存镜头到当前镜头的
  增量。
- `CameraData` 与对象相机来源切换只会重建基线，不会被当成切镜。
- v0.1.2 留下的 `Full` 初始旋转会自动迁移为 `YawOnly`。
- 场景加载结束后按加载状态或短暂稳定窗口恢复，不再依赖 KK_VR
  调用 `MoveToCurrent`。
- 普通基线重建、短暂丢失相机和 GripMove 不会重新触发绝对对齐。
- 已检查 v0.1.5 Release ZIP 的文件结构和其中 DLL 的哈希。

当前实验 DLL：

```text
文件名：KK_VR_CameraSync.dll
大小：32768 字节
SHA-256：2F893DE3CD99913743BAA86E1868830DC56E7AB617E1F7C0D3FDC20CCDE28671
```

## 构建环境限制

v0.1.5 已使用用户实际游戏目录中的这些程序集构建：

- `CharaStudio_Data\Managed\UnityEngine.dll`
- `CharaStudio_Data\Managed\Assembly-CSharp.dll`
- `BepInEx\core\BepInEx.dll`
- `BepInEx\core\0Harmony.dll`
- `BepInEx\VRGIN_KKCS.dll`

构建产物引用 CLR 2.0，并已确认不再引用缺失的
`0Harmony_BepInEx4.dll`。
- 根据已审查 KK_VR 源码建立的接口签名引用；

完成编译。

这能够验证：

- C# 源码结构；
- 使用到的接口形状；
- 目标 CLR 版本；
- 程序集引用名称；

这些检查不能代替对不同 KK、KK_VR、Timeline 和 SteamVR 组合进行更广泛
的实机兼容测试。

## 发布前必须完成

1. 使用源码中的 `build.ps1`，针对目标《恋活》安装目录重编译。
2. 确认引用的是 BepInEx 5 当前安装的 `BepInEx\core\0Harmony.dll`。
3. 确认实际 `VRGIN_KKCS.dll` 中存在：
   - `VRGIN.Core.VR.Active`
   - `VRGIN.Core.VR.Camera`
   - `VRCamera.Origin`
   - `VRCamera.Head`
4. 确认 KK_VR 中存在：
   - `KKCharaStudioVR.VRCameraMoveHelper`
   - `MoveToCurrent()`
   - `CurrentToCameraCtrl()`
5. 确认 `CameraControl.CameraData.distance` 的类型和语义与已审查源码一致。
6. 完成 README 中的全部测试项目。
7. 保存成功和失败场景的完整 BepInEx 日志。
8. 在完成更广泛的版本兼容测试前，将 v0.1.5 标记为实验版本。

## 首轮测试重点

### 普通连续运镜

预期：

- `AllMotion` 下 VR origin 按相机前后帧增量运动；
- 真实头显的相对转头和移动得到保留；
- 静止后不继续漂移。

### 切镜

预期：

- `CutsOnly` 只接受严格超过 `Position threshold` 的相邻帧位置变化；
- 缓慢运镜不会因累计距离最终超过阈值而被误判为切镜；
- 极端掉帧或单帧高速推拉仍可能被判断为切镜，这是当前算法限制。

### 旋转

预期：

- `YawOnly` 不倾斜 VR 世界；
- `Full` 会同步俯仰和翻滚，可能造成 VR 不适；
- `None` 不改变 VR origin 旋转。

### 保存与加载

预期：

- 保存时 HMD 写回 `CameraData` 不会触发额外同步；
- 加载过程中不跟随临时相机值；
- 场景卡加载完成后按保存的初始镜头执行一次绝对对齐；
- Timeline 已自动前进时，对齐后衔接到当前镜头；
- 加载后不会再次跳到陈旧 `CameraData`。

### 玩家主动移动

预期：

- 相机静止时，GripMove 不受影响；
- 相机同步期间，玩家相对于相机的偏移会被保留；
- 如果某个第三方插件与工作室相机在同一帧同时写入 VR origin，可能仍需
  移植 KKS 版本的 `ExternalVR` 驱动仲裁。

## 当前已知限制

- 已在用户的 KK/SteamVR 环境验证：插件加载、Timeline 运镜跟随和
  场景卡初始位置对齐可工作。
- 尚未验证所有 Timeline 版本和第三方相机轨道。
- 尚未完整移植 KKS 版本的 ExternalVR、VMD/VNGE 多驱动仲裁。
- 活动 `OCICamera` 依赖反射；不同 KK `Assembly-CSharp` 版本可能使用
  不同字段或属性名。
- Timeline 未安装时插件仍能运行；这也是设计目标。
