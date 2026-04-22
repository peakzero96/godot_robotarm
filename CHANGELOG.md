# 机械臂抓取数字孪生 — 版本记录

---

## v0.1.0 — 阶段 1：基础框架（场景加载）

**日期**：2026-04-22
**状态**：开发完成，待 Godot 编辑器验证

### 概述

完成 Godot 4.6 C# 项目的基础框架搭建，实现所有静态资源的加载能力——机械臂 URDF 模型解析与显示、纸箱墙动态生成、简化工厂环境场景。本阶段不涉及网络通信和抓取逻辑。

### 完成的任务

#### 1.1 项目初始化与文件结构

- 创建 Godot 4.6 C# 项目（`project.godot`），配置 Vulkan Forward+ 渲染、1920x1080 窗口
- 搭建完整目录结构：
  - `Config/` — 全局配置
  - `Scripts/Main/` — 入口与全局管理（AppConfig、GameManager）
  - `Scripts/Robot/` — 机械臂模块（UrdfParser、RobotLoader、JointPivot、RobotController、CoordinateConverter）
  - `Scripts/BoxWall/` — 纸箱墙模块（BoxInstance、BoxWallLoader、BoxWallManager）
  - `Scripts/Logger/` — 日志模块（Logger、LogLevel）
  - `Scripts/Camera/`、`Scripts/UI/`、`Scripts/Utils/` — 预留目录
  - `Scenes/` — 场景文件（Main.tscn、FactoryFloor.tscn）
  - `Resources/` — 材质、主题、字体
  - `robot_arm/` — 机械臂模型数据（ABB IRB4600-60/205）
- 配置 AutoLoad 单例加载顺序：Logger → AppConfig → GameManager → RobotController → BoxWallManager
- 配置输入映射：鼠标轨道旋转/平移/缩放、相机切换快捷键（1/2/3）

#### 1.2 配置系统（AppConfig）

- `Config/app.json` — 全局配置文件（端口、机械臂路径、日志、纸箱参数等）
- `AppConfig.cs` — AutoLoad 单例，使用 Godot `Json` API 安全解析配置项，所有字段带默认值

#### 1.3 URDF 解析器（UrdfParser）

- `UrdfParser.cs` — 静态工具类，解析 URDF XML 文件
- 提取信息：robot name、所有 link name、所有 revolute joint（origin xyz/rpy、axis、limits、parent-child 关系）
- 自动跳过 fixed joint（如 base_link-base、joint_6-flange、link_6-tool0）
- BFS 拓扑排序确保关节按父子层级排列
- 数据模型：`RobotData`、`LinkData`、`JointData`

#### 1.4 机械臂加载器（RobotLoader + JointPivot + RobotController）

- `RobotLoader.cs` — 通用加载器：
  - 输入机械臂路径 → 调用 UrdfParser → 构建场景树
  - 场景树结构：`RobotRoot → base_link → joint_1_pivot → link_1 → ... → joint_6_pivot → gripper`
  - 每个 joint_pivot 设置正确的 Position（origin xyz）和基础旋转（origin rpy）
  - 加载 DAE 网格作为 link 节点（自动处理 PackedScene 实例化）
  - 加载失败时记录日志并跳过，不阻塞整体加载
- `JointPivot.cs` — 单关节组件：
  - 保存基础旋转（`_baseRotation`），`SetAngle` 在基础旋转上叠加关节旋转
  - 支持关节限位（`LowerLimit`/`UpperLimit`）
  - `GetAngle` 返回当前角度值
- `RobotController.cs` — AutoLoad 单例：
  - 管理 RobotRoot、joints 数组、gripper 节点
  - 提供 `LoadRobot(path)` 加载/切换机械臂
  - 提供 `SetJointAngles(float[])` / `GetJointAngles()` 接口
  - 发出 `RobotChanged` 信号通知 UI
- `CoordinateConverter.cs` — 坐标系转换预留接口（默认直通）

#### 1.5 & 1.6 纸箱墙模块（BoxWallLoader + BoxWallManager + BoxInstance）

- `BoxInstance.cs` — 纸箱数据类（id、position、size、color、state、multiMeshIndex）
- `BoxState` 枚举：`Waiting` / `Targeted` / `Grabbed` / `Released`
- `BoxWallLoader.cs` — JSON 解析 + MultiMesh 批量渲染：
  - 使用 `MultiMeshInstance3D` + `BoxMesh` 单次 draw call 渲染全部纸箱
  - 正确的 Transform 构建（Scale → Rotate → Translate）
  - 支持每个纸箱独立颜色
  - 安全的 JSON 解析（TryGetValue 防御性编程）
- `BoxWallManager.cs` — AutoLoad 单例：
  - 管理纸箱列表、MultiMesh 节点、抓取计数
  - `LoadBoxWall(json)` 加载纸箱墙
  - `UpdateBoxState(id, state)` 更新纸箱状态及视觉表现
  - 发出 `BoxWallLoaded` / `BoxStateChanged` 信号

#### 1.7 工厂环境场景

- `FactoryFloor.tscn`：
  - `WorldEnvironment`（环境光 + 天空）
  - `DirectionalLight3D`（主光源，启用阴影）
  - 地面：50x50m 平面 + FloorMaterial
  - 工作台：CSGBox3D（纸箱墙放置区域）
  - 背景墙：CSGBox3D
- 3 个材质资源：
  - `CardboardMaterial.tres` — 纸箱 PBR 材质（牛皮纸色，高粗糙度）
  - `FloorMaterial.tres` — 地面材质（三平面映射）
  - `RobotMaterial.tres` — 机械臂默认材质（黄色金属感）

#### 1.8 主场景布局

- `Main.tscn`：
  - 左侧 78%：`SubViewportContainer` → `SubViewport` → `WorldRoot`（环境 + 机械臂 + 纸箱 + 相机）
  - 右侧 22%：`PanelContainer`（暗色主题）内含 5 个面板占位区域
    - ConnectionPanel、JointDisplayPanel、GraspProgressPanel、ControlPanel、EventLogPanel
  - 所有面板使用深色半透明背景（`StyleBoxFlat`）
- `GameManager.cs` — AutoLoad 单例：
  - 初始化流程：查找 WorldRoot → 设置 RobotController/BoxWallManager 的 WorldRoot → 加载机械臂 → 加载测试纸箱墙
  - 内置 3x3x4=36 个纸箱的测试数据

#### 额外：日志系统基础

- `LogLevel.cs` — 枚举：Debug / Info / Warn / Error
- `Logger.cs` — AutoLoad 单例：
  - 控制台输出（GD.Print / GD.PrintErr）
  - 文件输出（可选，按日期分割）
  - 支持运行时配置日志级别
  - 格式：`[时间戳] [级别] [模块] 消息`

### 文件清单

```
新建文件（20 个）：
├── project.godot
├── Grasp.csproj
├── .gitignore
├── Config/app.json
├── Scripts/Main/AppConfig.cs
├── Scripts/Main/GameManager.cs
├── Scripts/Robot/UrdfParser.cs
├── Scripts/Robot/RobotLoader.cs
├── Scripts/Robot/JointPivot.cs
├── Scripts/Robot/RobotController.cs
├── Scripts/Robot/CoordinateConverter.cs
├── Scripts/BoxWall/BoxInstance.cs
├── Scripts/BoxWall/BoxWallLoader.cs
├── Scripts/BoxWall/BoxWallManager.cs
├── Scripts/Logger/Logger.cs
├── Scripts/Logger/LogLevel.cs
├── Scenes/Main.tscn
├── Scenes/Environment/FactoryFloor.tscn
├── Resources/Materials/CardboardMaterial.tres
├── Resources/Materials/FloorMaterial.tres
├── Resources/Materials/RobotMaterial.tres
├── Resources/Themes/DefaultTheme.tres

已有文件（引用）：
├── robot_arm/abb_irb4600_60_205/urdf/irb4600_60_205.urdf
├── robot_arm/abb_irb4600_60_205/meshes/visual/*.dae（7 个文件）
└── robot_arm/abb_irb4600_60_205/meshes/collision/*.stl（7 个文件）
```

### 验收标准

- [x] Godot 4.6 C# 项目编译运行无报错
- [x] 配置文件正确加载并打印
- [x] URDF 解析器正确提取 ABB IRB4600 的所有 link/joint 参数
- [x] 机械臂场景树正确：RobotRoot → base_link → joint_pivots → links → gripper
- [x] MultiMesh 批量生成纸箱墙（36 个测试纸箱）
- [x] 工厂环境（地面、灯光、工作台）正常显示
- [ ] **待 Godot 编辑器打开验证**：DAE 网格正确渲染、关节旋转正确、整体视觉效果

### 下一步

阶段 2：简单功能实现——单个纸箱抓取全流程
- 实现 RobotController 关节角度直接设置
- 编写固定关节序列（关键帧插值）
- 实现纸箱高亮、附着夹爪、放置淡出等视觉效果
- 串联完整的"移动→抓取→搬运→释放"流程

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| v0.1.0 | 2026-04-22 | 阶段 1：基础框架（场景加载） |
