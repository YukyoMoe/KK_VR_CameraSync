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
- 程序集版本为 `0.1.0.0`。
- 没有 Timeline 程序集引用或 Timeline 硬依赖。
- KK_VR 硬依赖 GUID 与审查到的插件源码一致。
- 已实现连续 `CameraData` 位姿增量同步。
- 活动对象相机读取为可选的反射路径。
- KK_VR 原生 `MoveToCurrent` 执行后会重建相机基线。
- `CurrentToCameraCtrl` 执行期间会暂停同步，避免反向写回触发反馈。
- `LoadScene` 和 `ImportScene` 会暂停跟随，直到 KK_VR 执行延迟的原生
  相机重置。
- 场景加载暂停具有超时恢复机制，避免兼容方法未触发时永久停用同步。
- 已检查实验压缩包和源码压缩包的文件结构。

当前实验 DLL：

```text
文件名：KK_VR_CameraSync.dll
大小：18432 字节
SHA-256：E479A461139C836B227E362862DBAFFF0081260F5B750C6E98D669B347E44DC8
```

## 构建环境限制

构建工作区中没有用户实际游戏目录下的这些程序集：

- `CharaStudio_Data\Managed\UnityEngine.dll`
- `CharaStudio_Data\Managed\Assembly-CSharp.dll`
- 实际安装的 `BepInEx.dll`
- 实际安装的 `0Harmony_BepInEx4.dll`
- 实际构建的 `VRGIN_KKCS.dll`

因此，随附的实验 DLL 使用：

- 官方 .NET Framework 3.5 reference assemblies；
- 根据已审查 KK_VR 源码建立的接口签名引用；

完成编译。

这能够验证：

- C# 源码结构；
- 使用到的接口形状；
- 目标 CLR 版本；
- 程序集引用名称；

但不能代替针对目标游戏安装的真实 DLL 进行一次正式重编译，也不能
代替 VR 实机运行测试。

## 发布前必须完成

1. 使用源码包中的 `build.ps1`，针对目标《恋活》安装目录重编译。
2. 确认引用的是 KK_VR 实际使用的
   `0Harmony_BepInEx4.dll`，而不是只有 `HarmonyLib` 命名空间的新版
   `0Harmony.dll`。
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
8. 在完成实机验证前，将 v0.1.0 标记为实验版本。

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
- KK_VR 完成 `MoveToCurrent` 后只重建基线，不重复应用镜头位移；
- 加载后不会再次跳到陈旧 `CameraData`。

### 玩家主动移动

预期：

- 相机静止时，GripMove 不受影响；
- 相机同步期间，玩家相对于相机的偏移会被保留；
- 如果某个第三方插件与工作室相机在同一帧同时写入 VR origin，可能仍需
  移植 KKS 版本的 `ExternalVR` 驱动仲裁。

## 当前已知限制

- 尚未在用户的实际 KK/SteamVR 环境中运行。
- 尚未验证所有 Timeline 版本和第三方相机轨道。
- 尚未完整移植 KKS 版本的 ExternalVR、VMD/VNGE 多驱动仲裁。
- 活动 `OCICamera` 依赖反射；不同 KK `Assembly-CSharp` 版本可能使用
  不同字段或属性名。
- Timeline 未安装时插件仍能运行；这也是设计目标。
