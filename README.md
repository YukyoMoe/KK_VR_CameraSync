# KK VR Camera Sync

这是一个面向 `Ermin610/KK_VR` 的实验性伴随插件，用于让 KK_VR 的
VR tracking origin 跟随《恋活》工作室最终计算出的相机姿态。

插件不引用、也不要求安装 Timeline。它在 `LateUpdate` 中观察
`Studio.CameraControl.CameraData` 的最终结果，因此 Timeline 相机轨道、
手动工作室运镜，以及其他写入同一相机数据的插件都可以被间接检测。

## 当前功能范围

- 跟随 `CameraData` 的位置和旋转
- 通过反射选择性检测活动的 `OCICamera`
- 支持完整旋转、仅水平旋转或禁用旋转跟随
- 支持全部位移、仅切镜位移或禁用位置跟随
- 保留头显相对于动画镜头的实际追踪姿态
- 场景卡载入或导入完成后，按场景卡保存的初始镜头执行一次绝对位置对齐
- Timeline 自动播放已经前进时，先恢复初始镜头，再衔接已发生的镜头增量
- 进入空工作室时不强制移动头显
- 初始对齐可独立选择完整旋转、仅水平旋转或仅位置
- KK_VR 原生 `MoveToCurrent` 执行后自动重建同步基线
- KK_VR 将 HMD 姿态反向写回 `CameraData` 时暂停同步，防止反馈循环
- 场景加载和导入期间暂停同步
- 场景加载结束后自动恢复同步，不再等待 KK_VR 调用相机重置
- `CameraData` 与对象相机来源切换时只重建基线，避免被误判为切镜

如果 VMD/VNGE 只直接修改 `VRGIN_Camera (origin)`，而工作室相机保持
静止，本插件不会干预。若同一帧中既有插件直接修改 VR origin，又有
工作室相机运动，则尚未加入完整的多驱动仲裁；这属于首个验证版本的
已知范围限制。

## 安装

将：

```text
KK_VR_CameraSync.dll
```

复制到：

```text
Koikatu\BepInEx\plugins\KK_VR_CameraSync\
```

也可以解压 `KK_VR_CameraSync_v0.1.5_Experimental.zip`，然后把其中的
DLL 放入上述目录。

启动 CharaStudio VR。首次成功加载后将生成配置文件：

```text
BepInEx\config\yukyo.kkvr.camerasync.cfg
```

插件会在运行时检测以下 KK_VR 插件 ID：

```text
KKCharaStudioVRPlugin.KKCharaStudioVRPlugin
```

该依赖使用软检测，因为 Ermin 的工作室 VR 插件可能由
BepIn4Patcher 从 `BepInEx` 根目录转换和加载。

## 推荐初始配置

```ini
Enabled = true
Preserve head tracking = true
Align initial Studio camera = true
Initial alignment rotation mode = YawOnly
Rotation follow mode = YawOnly
Position follow mode = AllMotion
Position threshold = 2
Read active OCICamera = true
```

各项含义：

- `Enabled`：同步总开关。
- `Preserve head tracking`：保留玩家真实转头和小范围移动，建议开启。
- `Align initial Studio camera`：场景卡载入或导入完成后，只执行一次
  绝对位置对齐。对齐目标优先使用场景卡保存的初始镜头，因此不受
  Timeline 自动播放时机影响；进入空工作室、GripMove 和普通手动移动
  不会触发。
- `Initial alignment rotation mode`：
  - `YawOnly`：默认值，与 Ermin KK_VR 的直立 tracking origin 一致。
  - `Full`：尝试完整旋转，但可能被 Ermin KK_VR 再次直立化。
  - `None`：只对齐位置。
- `Rotation follow mode`：
  - `Full`：跟随俯仰、水平旋转和翻滚。
  - `YawOnly`：只跟随水平朝向，默认推荐。
  - `None`：不跟随相机旋转。
- `Position follow mode`：
  - `AllMotion`：跟随切镜、平移、推拉和相机抖动。
  - `CutsOnly`：只有相邻帧位置差超过阈值时才同步位置。
  - `Off`：不跟随相机位置，但仍可按旋转模式跟随朝向。
- `Position threshold`：`CutsOnly` 使用的相邻帧世界空间距离阈值。
- `Read active OCICamera`：如果当前 KK 的 `Assembly-CSharp` 暴露活动
  `OCICamera`，则优先读取该相机对象。
- `Toggle sync`：可选的同步开关键，默认未设置。

## 测试清单

建议每项单独测试并保留 BepInEx 日志。

1. 启动 CharaStudio VR，确认初始位置没有发生额外二次瞬移。
2. 使用普通工作室相机进行平移、旋转、环绕和推拉。
3. 播放包含相机位置、旋转和缩放轨道的动画。
4. 运镜期间实际转动、低头并小范围移动头显。
5. 测试两个距离较远的镜头之间的硬切换。
6. 同步期间使用 KK_VR 的 GripMove。
7. 保存场景，然后重新加载。
8. 导入场景。
9. 使用工作室相机槽位。
10. 点击 KK_VR 设置中的 `Reset Camera Position`。
11. 分别测试 `AllMotion`、`CutsOnly` 和 `Off`。
12. 分别测试 `Full`、`YawOnly` 和 `None`。
13. 关闭 `Preserve head tracking`，确认是否能接受逐帧锁定行为。
14. 关闭 `Enabled`，确认完全恢复 KK_VR 原始操作方式。
15. 如果使用 MMDD/VMD/VNGE，单独测试它们是否直接驱动 VR origin。

重点观察：

- 是否出现持续漂移；
- 是否发生一帧两次位移；
- 是否在保存后突然跳转；
- 是否在加载后先对齐、随后又跳到陈旧镜头；
- GripMove 是否被下一帧撤销；
- 静止相机是否产生细小抖动；
- 切镜是否按位置阈值正确识别；
- 退出 CharaStudio 后进程是否正常结束。

## 从源码编译

要求：

- 包含 CharaStudio 和 BepInEx 的《恋活》游戏目录
- BepInEx 5 自带的 `BepInEx\core\0Harmony.dll`
- Ermin 工作室 VR 使用的 `BepInEx\VRGIN_KKCS.dll`
- 带有 .NET Framework 3.5 targeting pack 的 MSBuild

在源码目录运行：

```powershell
.\build.ps1 -GameRoot "D:\Games\Koikatu"
```

或者直接调用 MSBuild：

```powershell
msbuild KK_VR_CameraSync.csproj /p:Configuration=Release `
  /p:GameRoot="D:\Games\Koikatu"
```

默认优先使用游戏中实际安装的 VRGIN：

```text
$(GameRoot)\BepInEx\VRGIN_KKCS.dll
```

如果该文件不存在，才回退到
`$(KKVRRoot)\VRGIN_KKCS\bin\Release\net35\VRGIN_KKCS.dll`。

输出文件为：

```text
bin\Release\net35\KK_VR_CameraSync.dll
```

插件不需要编译时引用 `KKCharaStudioVRPlugin.dll`。需要协调的
`VRCameraMoveHelper` 方法会在运行时按完整类型名查找并 Patch。

## 日志排查

正常加载时应看到类似：

```text
Loaded v0.1.5. Generic Studio camera observation is enabled;
Timeline is not a dependency.
```

如果兼容 Patch 失败，连续相机观察仍可能工作，但 KK_VR 原生重置后
可能需要手动关闭再开启同步。此时请保存完整日志，并核对：

- `VRCameraMoveHelper` 是否仍位于
  `KKCharaStudioVR.VRCameraMoveHelper`
- `MoveToCurrent()` 是否仍为无参数实例方法
- `CurrentToCameraCtrl()` 是否仍为无参数实例方法
- `Studio.LoadScene(string)` 和 `Studio.ImportScene(string)` 的签名是否改变

## 许可证

MIT。本实现复用了 `YukyoMoe/KKS_VR_TimelineCameraSync` 中的相机增量
变换、头显姿态保留和位置模式设计。
