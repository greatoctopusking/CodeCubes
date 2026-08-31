# VR 编程游戏 — 公示栏 + 垃圾桶（循环池）执行计划

> 最后更新：2026-09-01  
> 目标：实现「软木板取块 → 工作区拼程序 → 垃圾桶归还 → 换关清空」的**固定总量循环**系统。

---

## 一、整体设计

### 玩家体验

玩家在 VR 里从软木板抓取 3D 代码块，在工作区拼接程序控制机器人。不需要的块扔进垃圾桶；块**不会被销毁**，只在软木板与工作区之间流转。

### 核心规则：总量守恒

```
软木板上的块  +  工作区 / 手里的块  =  固定总数（由 Catalog 配置）
```

| 行为 | 效果 |
|------|------|
| 从软木板抓取 | 该槽位**变空**，**不会**自动补货 |
| 扔进垃圾桶 | 块**归还**到同类型的空槽位 |
| 换关 / 重试 / 离开关卡 | 工作区所有块**归还**软木板 |
| 程序运行中 | 垃圾桶**不可用** |
| 货架上的块 | 垃圾桶**忽略**（带 `CodeBlockShelfInstance`） |
| 同类型槽位已满 | **拒绝归还**（块不被销毁） |

### 与旧方案对比

| | 旧方案（无限供应） | 当前方案（循环池） |
|---|---|---|
| 抓取后 | 立刻补货 | 槽位变空 |
| 扔垃圾桶 | 销毁 | 归还槽位 |
| 换关 | 销毁工作区块 | 归还工作区块 |
| 总量 | 理论无限 | **固定**（Catalog `maxCount` 之和） |

---

## 二、阶段总览

| 阶段 | 名称 | 负责人 | 状态 |
|------|------|--------|------|
| **A** | 场景布置 | 你（Unity） | ✅ 已完成 |
| **B** | 数据配置（Catalog） | 代码 | ✅ 已完成 |
| **C** | 公示栏逻辑 | 代码 | ✅ 已完成 |
| **D** | 垃圾桶逻辑 | 代码 | ✅ 已完成 |
| **E** | 关卡联动 | 代码 | ✅ 已完成 |
| **F** | 场景接线与对齐 | 一起 | ✅ 已完成 |
| **G** | 测试验收 | 你（Play / VR） | ⏳ 自动项已过；G8 需 VR |
| **H** | 已知问题修复 | 代码（可选） | ✅ H1–H4 已处理；H5 可选 |

---

## 阶段 A — 场景布置 ✅

**目的：** 在 Unity 场景里摆好软木板、代码块、垃圾桶的物理布局。

| # | 任务 | 状态 | 说明 |
|---|------|------|------|
| A1 | 摆好软木板 `CodeBoard` | ✅ | 软木板 FBX，位置约 `(2.75, 1.07, 6.90)` |
| A2 | 代码块挂到软木板上 | ✅ | 15 种各 **3** 个，分文件夹 |
| A3 | 垃圾桶 + Trigger | ✅ | `BoxCollider`，`Is Trigger ✓`，无 Rigidbody |
| A4 | 保存场景 | ✅ | `Garage Scene.unity` |

### 推荐层级

```
CodeBoard
├── BoolBlocks
├── ControlBlocks
├── MoveBlocks
└── Others

TrashCan          ← BoxCollider (Is Trigger)
```

### 「每种多个」怎么摆

1. 选中块 → **Ctrl+D** 复制  
2. 仍放在 `CodeBoard` 下  
3. 打开 `Assets/Resources/CodeBlockCatalog.asset`  
4. 把对应类型的 **Max Count** 改成相同数量  

> **规则：软木板上摆了几个，Catalog 里 Max Count 就填几。**

---

## 阶段 B — 数据配置（Catalog）✅

**目的：** 用配置文件定义「有哪些块、每种几个」。

| # | 任务 | 状态 |
|---|------|------|
| B1 | 扩展 `CodeBlockCatalog`（`displayName` / `prefab` / `maxCount`） | ✅ |
| B2 | 填写各类型数量 | ✅ 各 **3**（与软木板场景实例数一致） |
| B3 | 15 种 prefab 引用完整 | ✅ 均指向 `Prefabs/CodeBlocks/` |

### 配置文件

```
Assets/Resources/CodeBlockCatalog.asset
```

### 当前配置（已与场景对齐）

| Display Name | Max Count | Prefab |
|--------------|-----------|--------|
| Start | 3 | `Prefabs/CodeBlocks/Others/Start.prefab` |
| MoveForward | 3 | `Prefabs/CodeBlocks/MoveBlocks/MoveForward.prefab` |
| TurnLeft | 3 | `Prefabs/CodeBlocks/MoveBlocks/TurnLeft.prefab` |
| TurnRight | 3 | `Prefabs/CodeBlocks/MoveBlocks/TurnRight.prefab` |
| While | 3 | `Prefabs/CodeBlocks/ControlBlocks/While.prefab` |
| WhileEnd | 3 | `Prefabs/CodeBlocks/ControlBlocks/WhileEnd.prefab` |
| IF | 3 | `Prefabs/CodeBlocks/ControlBlocks/IF.prefab` |
| IfEnd | 3 | `Prefabs/CodeBlocks/ControlBlocks/IfEnd.prefab` |
| Else | 3 | `Prefabs/CodeBlocks/ControlBlocks/Else.prefab` |
| True | 3 | `Prefabs/CodeBlocks/BoolBlocks/True.prefab` |
| False | 3 | `Prefabs/CodeBlocks/BoolBlocks/False.prefab` |
| DetectFrontStar | 3 | `Prefabs/CodeBlocks/BoolBlocks/DetectFrontStar.prefab` |
| DetectLeftStar | 3 | `Prefabs/CodeBlocks/BoolBlocks/DetectLeftStar.prefab` |
| DetectRightStar | 3 | `Prefabs/CodeBlocks/BoolBlocks/DetectRightStar.prefab` |
| StarRemain | 3 | `Prefabs/CodeBlocks/BoolBlocks/StarRemain.prefab` |
| **合计** | **45** | |

> **规则：软木板上摆了几个，Catalog 里 Max Count 就填几。** 当前板与 Catalog 均为各 3。

---

## 阶段 C — 公示栏逻辑 ✅

**目的：** 软木板管理槽位、抓取、归还。

| # | 任务 | 状态 | 说明 |
|---|------|------|------|
| C1 | `CodeBoard` 挂 `CodeBlockBoard` | ✅ | Catalog 已引用 |
| C2 | 槽位系统 | ✅ | 读取场景块建槽；有场景块时**禁止**回退生成 Cube |
| C3 | 抓取不补货 | ✅ | grab 后槽位变空；延迟一帧再 `SetParent(null)` |
| C4 | 块类型追踪 | ✅ | `BlockIdentity` 忽略大小写 + 全角后缀 + `Code` 类型映射；`CodeBlockPoolItem` 记录 prefab |
| C5 | `ReturnBlock()` | ✅ | 归还到同类型空槽 |
| C6 | 槽位已满 | ✅ | 无空槽时返回 false |

### 涉及脚本

| 脚本 | 作用 |
|------|------|
| `CodeBlockBoard.cs` | 槽位池、`ReturnBlock()`、`ClearWorkspace()` |
| `CodeBlockSlot.cs` | 单个槽位：放空 / 放回 |
| `CodeBlockShelfInstance.cs` | 货架标记 |
| `CodeBlockPoolItem.cs` | 所属 prefab 标记 |
| `BlockIdentity.cs` | 名称 / 类型匹配 |

### 抓取状态变化

```
货架上（静态）              被抓取后（动态）
─────────────────────      ─────────────────────
isKinematic = true     →   isKinematic = false
useGravity = false     →   useGravity = true
有 ShelfInstance       →   移除 ShelfInstance
槽位占用               →   槽位变空（不补货）
```

---

## 阶段 D — 垃圾桶逻辑 ✅

**目的：** 扔进桶 → 归还软木板，不销毁。

| # | 任务 | 状态 |
|---|------|------|
| D1 | `TrashCan` 挂 `TrashCan` 组件 | ✅ |
| D2 | 归还代替销毁 | ✅ |
| D3 | 运行中禁用 | ✅ |
| D4 | 忽略货架块 | ✅ |

### 归还流程

```
块进入 Trigger
  → 是货架块？ → 忽略
  → 正在运行程序？ → 忽略
  → 若仍被抓取：等待 selectExited
  → 若已松开（扔入）：立即归还
  → ConnectionManager.CleanupBlock() 断线
  → CodeBlockBoard.ReturnBlock() 送回空槽
  → 无空槽 → 拒绝归还（不 Destroy）
```

### Trigger 参考

- 本地 Size 约 `(0.004, 0.005, 0.004)`  
- 模型缩放约 `(300, 300, 175)`  
- 世界尺寸约 `(1.2, 1.5, 0.7)` — 一般够用  
- 若扔入无反应：在 Scene 视图看绿色线框是否盖住桶口，不够就调大 Size  

---

## 阶段 E — 关卡联动 ✅

**目的：** 换关时工作区块归还软木板。

| # | 任务 | 状态 | 说明 |
|---|------|------|------|
| E1 | `ClearWorkspace()` 改为归还 | ✅ | 无空槽时才 Destroy |
| E2 | 接入 `LevelManager` | ✅ | `LoadLevelByIndex` / `StopLevel` |
| E3 | 归还前断线 | ✅ | `CleanupBlock()` |

---

## 阶段 F — 场景接线与对齐 ✅

| # | 任务 | 状态 | 说明 |
|---|------|------|------|
| F1 | `CodeBoard` ↔ `CodeBlockBoard` | ✅ | |
| F2 | `TrashCan` ↔ `TrashCan` | ✅ | |
| F3 | Catalog 与软木板数量对齐 | ✅ | 各 3，共 45 |
| F4 | 槽位位置 | ✅ | 使用场景摆放位置 |
| F5 | GUID / Build Settings | ✅ | `Garage Scene` 已进 Build Settings |

---

## 阶段 G — 测试验收 ⏳

**说明：** G1/G2/G5/G6/G9 可由 Edit Mode 批测覆盖；G3/G4/G7/G8 需 Play / VR。

菜单：`VRPG → Validate Pool System (Final D-G)`  
批测：`-executeMethod PoolSystemFinalValidation.ValidateAndExit` → `Logs/PoolSystemFinalValidation.txt`

| # | 测试项 | 操作 | 预期 | 结果 |
|---|--------|------|------|------|
| G1 | 抓取 | 从软木板抓一块 | 槽位变空，不自动补货 | ✅ 自动 |
| G2 | 扔桶归还 | 扔进垃圾桶 | 回到同类型空槽，变静态 | ✅ 自动（ReturnBlock） |
| G3 | 连续丢弃 | 抓出再扔回多次 | 每次归还，总量不变 | ⬜ VR |
| G4 | 全部取出 | 某类型全部抓出 | 该类型槽位全空 | ⬜ VR |
| G5 | 槽位已满 | 无空槽时扔入 | 拒绝归还 | ✅ 自动 |
| G6 | 换关 | 抓几块后切换关卡 | 工作区清空，块回软木板 | ✅ 自动（ClearWorkspace） |
| G7 | 运行中扔桶 | 程序执行时扔块 | 无效 | ⬜ VR（代码已有 IsExecuting 锁） |
| G8 | 拼程序运行 | 连接后左手运行 | 连接与执行正常 | ⬜ VR 必测 |
| G9 | 总量守恒 | 任意组合后统计 | 软木板 + 工作区 = TotalBlockCount | ✅ 自动 |

### Unity 刷新提示

场景被外部修改时弹窗 **"The open scene(s) have been modified externally"** → 点 **Reload**。  
脚本改动一般只需等编译完成，不必重开整个项目。

---

## 阶段 H — 已知问题（复查发现）✅（核心项）

| # | 严重度 | 问题 | 状态 |
|---|--------|------|------|
| H1 | 中高 | 根节点多余转向块 | ✅ 现为 CodeBoard 下空文件夹（组织用），非额外可抓块 |
| H2 | 中 | grab 时立刻 `SetParent(null)` 抢父子关系 | ✅ 延迟一帧解绑 |
| H3 | 低 | `HasExistingVisual` 漏检根 Renderer | ✅ 已含根节点 |
| H4 | 低 | `IsUnderBoard` 死代码 | ✅ 已移除 |
| H5 | 低 | 中文目录与 `Assets/Art/` 双份资源 | ⏳ 可选统一 |

---

## 三、脚本一览

| 脚本 | 路径 | 作用 | 阶段 |
|------|------|------|------|
| `CodeBlockCatalog.cs` | `Assets/scripts/` | 块类型 + 数量配置 | B |
| `CodeBlockBoard.cs` | `Assets/scripts/` | 公示栏总管 | C, E |
| `CodeBlockSlot.cs` | `Assets/scripts/` | 单个槽位 | C |
| `CodeBlockShelfInstance.cs` | `Assets/scripts/` | 货架标记 | C |
| `CodeBlockPoolItem.cs` | `Assets/scripts/` | 所属 prefab 标记 | C |
| `BlockIdentity.cs` | `Assets/scripts/` | 名称匹配 | C |
| `TrashCan.cs` | `Assets/scripts/` | 垃圾桶归还 | D |
| `ConnectionManager.cs` | `Assets/scripts/` | `CleanupBlock` 断线 | D, E |
| `LevelManager.cs` | `Assets/scripts/` | 换关清工作区 | E |

---

## 四、推荐执行顺序

```
A–F ✅
  ↓
H2–H4 ✅
  ↓
G 自动项 ✅（批测）
  ↓
G3 / G4 / G7 / G8  VR 手测  ← 当前主线
  ↓
H1 / H5 可选清理
```

---

## 五、关键文件路径

| 文件 | 路径 |
|------|------|
| 场景 | `Assets/Scenes/Garage Scene.unity` |
| Catalog | `Assets/Resources/CodeBlockCatalog.asset` |
| 代码块 Prefab | `Assets/Prefabs/CodeBlocks/` |
| 脚本 | `Assets/scripts/` |
| 软木板（场景引用） | `Assets/软木板公告板(...)/Cork_Bulletin_Board.fbx` |
| 垃圾桶（场景引用） | `Assets/垃圾桶10(...)/.../TrashCan.fbx` |
| Art 副本（备用） | `Assets/Art/CorkBoard/`、`Assets/Art/TrashCan/` |
| 批测日志 | `Logs/CodeBlockBoardValidation.txt`、`Logs/PoolSystemFinalValidation.txt` |

---

## 六、下一步建议

1. Unity 中 Reload 场景（若提示外部修改）  
2. 菜单跑 `VRPG → Validate Pool System (Final D-G)`，确认 PASS  
3. **VR 手测 G8**：拼一条程序、左手运行  
4. 顺带确认 G3 / G4 / G7；若总量异常再查 H1
