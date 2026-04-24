# 机械臂抓取数字孪生 — 版本记录

---

## v0.1.0 — 阶段 1：基础框架（场景加载）

**日期**：2026-04-22
**状态**：已通过 Godot 运行验证

---

### 1. 启动与初始化流程

#### 1.1 Godot 引擎启动入口

```
project.godot
├── [application] run/main_scene = "res://Scenes/Main.tscn"
├── [autoload] 按以下顺序创建 AutoLoad 单例：
│   ① Logger
│   ② AppConfig
│   ③ GameManager
│   ④ RobotController
│   ⑤ BoxWallManager
└── [display] window/size/mode=2 (带边框窗口 1280x720)
```

#### 1.2 AutoLoad 初始化时序（严格按此顺序执行）

```
Godot 启动
 │
 ├─ ① Logger._Ready()                   ← Scripts/Logger/Logger.cs:14
 │    └─ 设置 Instance 单例引用
 │       初始级别: INFO, 文件日志: 关闭
 │
 ├─ ② AppConfig._Ready()                ← Scripts/Main/AppConfig.cs:34
 │    ├─ 设置 Instance 单例引用
 │    └─ LoadConfig()                    ← Scripts/Main/AppConfig.cs:40
 │       ├─ 读取 "res://Config/app.json"
 │       ├─ Json.ParseString → AsGodotDictionary
 │       └─ 解析 5 个配置段:
 │          ├─ network  → ListenPort(5005), MaxConnections(1), HeartbeatIntervalMs(1000), ReconnectTimeoutMs(5000)
 │          ├─ robot    → RobotPath("robot_arm/abb_irb4600_60_205"), UpdateRateHz(60)
 │          ├─ scene    → DefaultCamera("free_orbit"), EnvironmentPath("Scenes/Environment/FactoryFloor.tscn")
 │          ├─ logging  → LogLevelStr("INFO"), FileLoggingEnabled(true), LogFilePath, UiMaxLines(500)
 │          └─ box_wall → BoxDefaultColor("#C4A882"), BoxGrabbedColor("#4CAF50"), FadeOutDurationMs(2000)
 │
 ├─ ③ GameManager._Ready()              ← Scripts/Main/GameManager.cs:15
 │    ├─ 设置 Instance 单例引用
 │    └─ CallDeferred(MethodName.Initialize)  ← 延迟到下一帧执行
 │
 ├─ ④ RobotController._Ready()          ← Scripts/Robot/RobotController.cs:25
 │    └─ 设置 Instance 单例引用
 │       (此时 joints 为空数组, 等待 GameManager 调用 LoadRobot)
 │
 ├─ ⑤ BoxWallManager._Ready()           ← Scripts/BoxWall/BoxWallManager.cs:26
 │    └─ 设置 Instance 单例引用
 │       (此时 boxes 为空字典, 等待 GameManager 调用 LoadBoxWall)
 │
 ├─ Godot 加载 Main.tscn 场景树
 │    ├─ Main (Control)
 │    │   ├─ ViewportContainer → SubViewport
 │    │   │   └─ WorldRoot (Node3D, unique_name_in_owner)
 │    │   │       ├─ Environment (FactoryFloor.tscn 实例)
 │    │   │       ├─ Camera3D + FreeOrbitCamera 脚本
 │    │   │       ├─ RobotRoot (稍后由 RobotController 动态添加)
 │    │   │       └─ BoxWall (稍后由 BoxWallManager 动态添加)
 │    │   └─ UIPanel → VBox (5 个占位面板)
 │    └─ FreeOrbitCamera._Ready()        ← Scripts/Camera/FreeOrbitCamera.cs:22
 │
 └─ 下一帧: GameManager.Initialize()     ← Scripts/Main/GameManager.cs:23
     ├─ Logger.Info("GameManager", "Initializing...")
     ├─ GetTree().CurrentScene.FindChild("WorldRoot", recursive: true)
     ├─ RobotController.SetWorldRoot(worldRoot)    ← Scripts/Robot/RobotController.cs:30
     ├─ BoxWallManager.SetWorldRoot(worldRoot)     ← Scripts/BoxWall/BoxWallManager.cs:31
     │
     ├─ RobotController.LoadRobot()                ← Scripts/Robot/RobotController.cs:35
     │   ├─ RobotLoader.Load(robotPath)            ← Scripts/Robot/RobotLoader.cs:17
     │   │   ├─ UrdfParser.Parse(robotPath)        ← Scripts/Robot/UrdfParser.cs:35
     │   │   │   ├─ FindFirstUrdf()                ← 扫描 urdf/ 目录找第一个 .urdf
     │   │   │   ├─ XmlDocument.LoadXml()          ← 解析 XML
     │   │   │   ├─ 提取所有 <link> → LinkData[]
     │   │   │   ├─ 提取 revolute/continuous/prismatic <joint> → JointData[]
     │   │   │   │   └─ 每个 JointData: name, type, parent_link, child_link,
     │   │   │   │      origin_xyz, origin_rpy, axis, lower, upper
     │   │   │   └─ TopologicalSort()              ← BFS 确保父关节在子关节之前
     │   │   │
     │   │   ├─ 创建 RobotRoot (Node3D)
     │   │   ├─ 找到 base_link (不是任何 revolute joint 的 child)
     │   │   ├─ LoadLinkNode("base_link", path)    ← 加载 DAE → MeshInstance3D
     │   │   ├─ 遍历 joints (拓扑序):
     │   │   │   ├─ 创建 JointPivot (Node3D)
     │   │   │   │   ├─ Position = joint.origin_xyz
     │   │   │   │   └─ SetBaseRotation(joint.origin_rpy)
     │   │   │   ├─ LoadLinkNode(child_link, path)  ← 加载 DAE 网格
     │   │   │   └─ pivot.AddChild(childMesh)
     │   │   └─ 最后一个 joint 下创建 gripper (Node3D)
     │   │
     │   ├─ worldRoot.AddChild(RobotRoot)
     │   ├─ RobotRoot.Position = (0, 0, 0)
     │   ├─ RobotRoot.Rotation = (90°, 0, 0)      ← 用户调整：URDF 坐标系 → Godot 坐标系
     │   └─ EmitSignal(RobotChanged, name, jointCount)
     │
     └─ LoadTestBoxWall()                       ← Scripts/Main/GameManager.cs:54
         ├─ 生成 3x3x4 = 36 个纸箱 JSON 数据
         │   (位置偏移到工作台顶面: baseX=0.7, baseY=0.81, baseZ=-1.3)
         └─ BoxWallManager.LoadBoxWall(json)      ← Scripts/BoxWall/BoxWallManager.cs:36
             ├─ BoxWallLoader.Load(json)          ← Scripts/BoxWall/BoxWallLoader.cs:16
             │   ├─ Json.ParseString → 遍历 boxes 数组
             │   ├─ 每个 box → BoxInstance (id, position, size, color, multiMeshIndex)
             │   └─ CreateMultiMesh(boxes)
             │       ├─ new MultiMesh (TransformFormat=3D, UseColors=true)
             │       ├─ InstanceCount = boxes.Count
             │       └─ 循环 SetInstanceTransform + SetInstanceColor
             │
             ├─ worldRoot.AddChild(meshInstance)
             └─ EmitSignal(BoxWallLoaded, totalCount)
```

#### 1.3 运行时每帧循环

```
_process(delta) 每帧:
 │
 ├─ FreeOrbitCamera._UnhandledInput()       ← Scripts/Camera/FreeOrbitCamera.cs:33
 │   ├─ 左键拖拽 + Input("orbit_rotate") → 更新 _yaw/_pitch → UpdateTransform()
 │   ├─ 右键拖拽 + Input("orbit_pan")    → 更新 _target → UpdateTransform()
 │   ├─ 滚轮                               → 更新 _distance → UpdateTransform()
 │   └─ 中键                               → ResetCamera()
 │
 └─ (阶段 2+ 将在此处添加: 关节角度更新、状态机驱动等)
```

---

### 2. 模块函数引用

#### 2.1 Logger 模块 — `Scripts/Logger/`

| 类 | 类型 | 文件 |
|---|---|---|
| `LogLevel` | `enum` | LogLevel.cs |
| `Logger` | `partial class : Node` (AutoLoad) | Logger.cs |

```
Logger (AutoLoad 单例)
├── static Instance : Logger
├── Configure(level, fileEnabled, filePath)  ← 运行时配置
├── Debug(source, message)
├── Info(source, message)
├── Warn(source, message)
├── Error(source, message)
└── private Log(level, source, message)
    ├── 格式化: "[时间戳] [级别] [模块] 消息"
    ├── GD.Print / GD.PrintErr
    └── WriteToFile(line) → Logs/grasp_YYYY-MM-DD.log
```

#### 2.2 Main 模块 — `Scripts/Main/`

| 类 | 类型 | 文件 |
|---|---|---|
| `AppConfig` | `partial class : Node` (AutoLoad) | AppConfig.cs |
| `GameManager` | `partial class : Node` (AutoLoad) | GameManager.cs |

```
AppConfig (AutoLoad 单例)
├── static Instance : AppConfig
├── 属性 (从 app.json 读取, 带默认值):
│   ├── ListenPort : int = 5005
│   ├── MaxConnections : int = 1
│   ├── HeartbeatIntervalMs : int = 1000
│   ├── ReconnectTimeoutMs : int = 5000
│   ├── RobotPath : string = "robot_arm/abb_irb4600_60_205"
│   ├── UpdateRateHz : int = 60
│   ├── DefaultCamera : string = "free_orbit"
│   ├── EnvironmentPath : string
│   ├── LogLevelStr : string = "INFO"
│   ├── FileLoggingEnabled : bool
│   ├── LogFilePath : string
│   ├── UiMaxLines : int = 500
│   ├── BoxDefaultColor : string = "#C4A882"
│   ├── BoxGrabbedColor : string = "#4CAF50"
│   └── FadeOutDurationMs : int = 2000
├── _Ready() → Instance = this; LoadConfig()
└── LoadConfig() → 读取 app.json → 填充属性

GameManager (AutoLoad 单例)
├── static Instance : GameManager
├── _Ready() → Instance = this; CallDeferred(Initialize)
├── Initialize()
│   ├── 查找 WorldRoot (递归搜索场景树)
│   ├── RobotController.SetWorldRoot(worldRoot)
│   ├── BoxWallManager.SetWorldRoot(worldRoot)
│   ├── RobotController.LoadRobot()
│   └── LoadTestBoxWall() → BoxWallManager.LoadBoxWall(json)
└── LoadTestBoxWall() → 生成 36 纸箱测试 JSON
```

#### 2.3 Robot 模块 — `Scripts/Robot/`

| 类 | 类型 | 文件 |
|---|---|---|
| `LinkData` | `class` | UrdfParser.cs |
| `JointData` | `class` | UrdfParser.cs |
| `RobotData` | `class` | UrdfParser.cs |
| `UrdfParser` | `static class` | UrdfParser.cs |
| `RobotLoadResult` | `class` | RobotLoader.cs |
| `RobotLoader` | `static class` | RobotLoader.cs |
| `JointPivot` | `partial class : Node3D` | JointPivot.cs |
| `RobotController` | `partial class : Node` (AutoLoad) | RobotController.cs |
| `CoordinateConverter` | `static class` | CoordinateConverter.cs |

```
UrdfParser (静态工具类)
├── Parse(robotPath) → RobotData?
│   ├── FindFirstUrdf(urdfDir) → string?        ← 扫描目录找 .urdf
│   ├── XmlDocument.LoadXml()                    ← 解析 XML
│   ├── 提取 <link> → LinkData[]
│   ├── 提取 <joint type="revolute|continuous|prismatic"> → JointData[]
│   │   └── 每个 JointData: Name, Type, ParentLink, ChildLink,
│   │      OriginXyz, OriginRpy, Axis, Lower, Upper
│   ├── ParseVec3(value) → Vector3               ← "x y z" → Vector3
│   └── TopologicalSort(joints, links) → JointData[]  ← BFS 拓扑排序
└── 数据模型:
    ├── LinkData { Name }
    ├── JointData { Name, Type, ParentLink, ChildLink, OriginXyz, OriginRpy, Axis, Lower, Upper }
    └── RobotData { Name, Links[], Joints[], JointCount }

RobotLoader (静态工具类)
├── Load(robotPath) → RobotLoadResult?
│   ├── UrdfParser.Parse(robotPath) → RobotData
│   ├── 创建 RobotRoot (Node3D)
│   ├── 找 base_link (不是任何 revolute joint 的 child link)
│   ├── LoadLinkNode(linkName, path) → Node3D?   ← 加载 DAE 网格
│   │   └── ResourceLoader.Load<PackedScene>(dae).Instantiate()
│   ├── 遍历 joints (拓扑序):
│   │   ├── new JointPivot { Position, RotationAxis, Limits }
│   │   ├── pivot.SetBaseRotation(rpy)
│   │   └── pivot.AddChild(childMesh)
│   └── new Node3D("gripper") → joints[last].AddChild
└── RobotLoadResult { RootNode, Joints[], JointCount, Gripper, RobotName }

JointPivot (Node3D 组件, 附加到每个关节枢轴)
├── JointName : string
├── RotationAxis : Vector3 = Up
├── LowerLimit / UpperLimit : float
├── SetBaseRotation(rpy) → _baseRotation = Basis.FromEuler(rpy)
├── SetAngle(radians) → Basis = _baseRotation.Rotated(Axis, clamped)
└── GetAngle() → _currentAngle

RobotController (AutoLoad 单例)
├── static Instance : RobotController
├── 属性: JointCount, Joints[], Gripper, RobotName, RobotRoot
├── [Signal] RobotChanged(robotName, jointCount)
├── _Ready() → Instance = this
├── SetWorldRoot(worldRoot)
├── LoadRobot(path?) → bool
│   ├── RobotLoader.Load(path) → RobotLoadResult
│   ├── worldRoot.AddChild(RobotRoot)
│   ├── RobotRoot.Position / Rotation 设置
│   └── EmitSignal(RobotChanged)
├── SetJointAngles(float[]) → 循环调用 joints[i].SetAngle()
└── GetJointAngles() → float[]

CoordinateConverter (静态工具类, 预留接口)
├── ConvertPosition(position) → position (直通)
└── ConvertRotation(euler) → euler (直通)
```

#### 2.4 BoxWall 模块 — `Scripts/BoxWall/`

| 类 | 类型 | 文件 |
|---|---|---|
| `BoxState` | `enum` | BoxInstance.cs |
| `BoxInstance` | `class` | BoxInstance.cs |
| `BoxWallLoadResult` | `class` | BoxWallLoader.cs |
| `BoxWallLoader` | `static class` | BoxWallLoader.cs |
| `BoxWallManager` | `partial class : Node` (AutoLoad) | BoxWallManager.cs |

```
BoxState (枚举)
└── Waiting → Targeted → Grabbed → Released

BoxInstance (数据类)
├── Id : int
├── Position : Vector3
├── RotationDeg : Vector3
├── Size : Vector3
├── Color : Color
├── State : BoxState = Waiting
└── MultiMeshIndex : int

BoxWallLoader (静态工具类)
├── Load(jsonData) → BoxWallLoadResult?
│   ├── Json.ParseString → 遍历 boxes[]
│   │   └── 每个 → BoxInstance { Id, Position, RotationDeg, Size, Color, MultiMeshIndex }
│   ├── GetNum(dict, key, fallback) → float    ← 安全取数值
│   ├── ParseColor(hex) → Color
│   └── CreateMultiMesh(boxes) → BoxWallLoadResult
│       ├── new MultiMesh (TransformFormat=3D, UseColors=true)
│       ├── InstanceCount = boxes.Count
│       └── 循环: SetInstanceTransform + SetInstanceColor
└── BoxWallLoadResult { MeshInstance, Boxes[], TotalCount }

BoxWallManager (AutoLoad 单例)
├── static Instance : BoxWallManager
├── 属性: TotalCount, GrabbedCount, RemainingCount
├── [Signal] BoxWallLoaded(totalCount)
├── [Signal] BoxStateChanged(boxId, newState)
├── _Ready() → Instance = this
├── SetWorldRoot(worldRoot)
├── LoadBoxWall(jsonData) → bool
│   ├── ClearWall()
│   ├── BoxWallLoader.Load(jsonData) → BoxWallLoadResult
│   ├── worldRoot.AddChild(meshInstance)
│   └── EmitSignal(BoxWallLoaded)
├── GetBox(id) → BoxInstance?
├── UpdateBoxState(id, newState)
│   ├── 更新 box.State
│   ├── 更新 _grabbedCount
│   ├── UpdateBoxVisual(box)
│   │   ├── Waiting   → 恢复原始颜色
│   │   ├── Targeted  → 颜色变亮 (Lightened 0.3)
│   │   └── Grabbed/Released → 隐藏 (scale=0)
│   └── EmitSignal(BoxStateChanged)
├── ResetProgress()
└── ClearWall() → QueueFree + 清空字典
```

#### 2.5 Camera 模块 — `Scripts/Camera/`

| 类 | 类型 | 文件 |
|---|---|---|
| `FreeOrbitCamera` | `partial class : Camera3D` | FreeOrbitCamera.cs |

```
FreeOrbitCamera (Camera3D 子类, 附加到场景 Camera3D 节点)
├── [Export] RotateSpeed=0.005, ZoomSpeed=0.1, PanSpeed=0.005
├── [Export] MinDistance=1, MaxDistance=50
├── [Export] MinPitch/MaxPitch (±89.4°)
├── _Ready() → 从初始朝向计算 _yaw, _pitch, _target, _distance
├── _UnhandledInput(event)
│   ├── 左键拖拽 (orbit_rotate) → _yaw/_pitch → UpdateTransform()
│   ├── 右键拖拽 (orbit_pan)    → _target 平移 → UpdateTransform()
│   ├── 滚轮上 (orbit_zoom)     → _distance 减小
│   ├── 滚轮下 (orbit_zoom)     → _distance 增大
│   └── 中键 (orbit_reset)      → ResetCamera()
├── UpdateTransform() → Position = target + offset; LookAt(target)
└── ResetCamera() → 恢复初始位置和朝向
```

---

### 3. 场景树结构

```
Main (Control) — Scenes/Main.tscn
├── ViewportContainer
│   └── SubViewport
│       └── WorldRoot (Node3D, unique_name_in_owner)
│           ├── Environment (Node3D) — FactoryFloor.tscn 实例化
│           │   ├── WorldEnvironment
│           │   ├── DirectionalLight3D (阴影启用)
│           │   ├── Floor (MeshInstance3D + PlaneMesh 50x50)
│           │   ├── Workbench (CSGBox3D) @ (2, 0.4, -0.5) size 3x0.8x2
│           │   └── BackgroundWall (CSGBox3D) @ (2, 2, -1.5) size 3x3x0.1
│           │
│           ├── Camera3D [FreeOrbitCamera] @ (5, 4, 8)
│           │
│           ├── RobotRoot (Node3D) @ (0, 0, 0) — RobotController 动态加载
│           │   └── base_link (MeshInstance3D)
│           │       └── joint_1_pivot [JointPivot] @ (0, 0, 0.495) axis=(0,0,1)
│           │           └── link_1 (MeshInstance3D)
│           │               └── joint_2_pivot [JointPivot] @ (0.175, 0, 0) axis=(0,1,0)
│           │                   └── link_2 (MeshInstance3D)
│           │                       └── joint_3_pivot [JointPivot] @ (0, 0, 0.9) axis=(0,1,0)
│           │                           └── link_3 (MeshInstance3D)
│           │                               └── joint_4_pivot [JointPivot] @ (0, 0, 0.175) axis=(1,0,0)
│           │                                   └── link_4 (MeshInstance3D)
│           │                                       └── joint_5_pivot [JointPivot] @ (0.96, 0, 0) axis=(0,1,0)
│           │                                           └── link_5 (MeshInstance3D)
│           │                                               └── joint_6_pivot [JointPivot] @ (0.135, 0, 0) axis=(1,0,0)
│           │                                                   └── link_6 (MeshInstance3D)
│           │                                                       └── gripper (Node3D)
│           │
│           └── BoxWall (MultiMeshInstance3D) — BoxWallManager 动态加载
│               └── MultiMesh (36 instances, BoxMesh)
│
└── UIPanel (PanelContainer)
    └── VBox (VBoxContainer)
        ├── ConnectionPanel  (占位)
        ├── JointDisplayPanel (占位)
        ├── GraspProgressPanel (占位)
        ├── ControlPanel (占位)
        └── EventLogPanel (占位)
```

---

### 4. 信号连接图

```
RobotController
└── [Signal] RobotChanged(robotName: string, jointCount: int)
    └── (阶段 6 UI 订阅: 更新 JointDisplayPanel)

BoxWallManager
├── [Signal] BoxWallLoaded(totalCount: int)
│   └── (阶段 6 UI 订阅: 更新 GraspProgressPanel)
└── [Signal] BoxStateChanged(boxId: int, newState: int)
    └── (阶段 6 UI 订阅: 更新 EventLogPanel)
```

---

### 5. 配置文件引用

```
Config/app.json
├── network.listen_port         → AppConfig.ListenPort
├── network.max_connections     → AppConfig.MaxConnections
├── network.heartbeat_interval_ms → AppConfig.HeartbeatIntervalMs
├── network.reconnect_timeout_ms  → AppConfig.ReconnectTimeoutMs
├── robot.default_path          → AppConfig.RobotPath
├── robot.update_rate_hz        → AppConfig.UpdateRateHz
├── scene.default_camera        → AppConfig.DefaultCamera
├── scene.environment           → AppConfig.EnvironmentPath
├── logging.level               → AppConfig.LogLevelStr
├── logging.file_enabled        → AppConfig.FileLoggingEnabled
├── logging.file_path           → AppConfig.LogFilePath
├── logging.ui_max_lines        → AppConfig.UiMaxLines
├── box_wall.default_color      → AppConfig.BoxDefaultColor
├── box_wall.grabbed_color      → AppConfig.BoxGrabbedColor
└── box_wall.fade_out_duration_ms → AppConfig.FadeOutDurationMs
```

---

### 6. 文件清单

```
Scripts/
├── Main/
│   ├── AppConfig.cs         ← AutoLoad #2, 读取 app.json
│   └── GameManager.cs       ← AutoLoad #3, 初始化入口, 加载机械臂+纸箱墙
├── Robot/
│   ├── UrdfParser.cs        ← 静态类, URDF XML → RobotData
│   ├── RobotLoader.cs       ← 静态类, RobotData → 场景树 Node3D
│   ├── JointPivot.cs        ← Node3D, 单关节旋转组件
│   ├── RobotController.cs   ← AutoLoad #4, 机械臂生命周期管理
│   └── CoordinateConverter.cs ← 静态类, 坐标转换预留
├── BoxWall/
│   ├── BoxInstance.cs       ← 数据类 + BoxState 枚举
│   ├── BoxWallLoader.cs     ← 静态类, JSON → MultiMeshInstance3D
│   └── BoxWallManager.cs    ← AutoLoad #5, 纸箱墙状态管理
├── Logger/
│   ├── Logger.cs            ← AutoLoad #1, 日志核心
│   └── LogLevel.cs          ← 枚举
├── Camera/
│   └── FreeOrbitCamera.cs   ← Camera3D, 轨道相机
├── UI/                      ← 预留
└── Utils/                   ← 预留

Scenes/
├── Main.tscn                ← 主场景 (3D 视口 + UI 面板)
└── Environment/
    └── FactoryFloor.tscn    ← 工厂环境 (地面+灯光+工作台)

Config/
└── app.json                 ← 全局配置

Resources/
├── Materials/
│   ├── CardboardMaterial.tres
│   ├── FloorMaterial.tres
│   └── RobotMaterial.tres
└── Themes/
    └── DefaultTheme.tres

robot_arm/
└── abb_irb4600_60_205/
    ├── urdf/irb4600_60_205.urdf
    └── meshes/
        ├── visual/*.dae (7)
        └── collision/*.stl (7)
```

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| v0.1.0 | 2026-04-22 | 阶段 1：基础框架（场景加载） |
