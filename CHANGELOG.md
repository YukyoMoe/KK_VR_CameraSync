# 更新日志

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
