# GetStateList 如何把 Inspector 中配置的状态类名实例化成状态对象

完整链路如下。

## 1. Inspector 中的状态配置

Inspector 里的字段在：

```text
Assets/PLAYER TWO/Platformer Project/Scripts/Player/PlayerStateManager.cs:26
```

```csharp
[ClassTypeName(typeof(PlayerState))]
public string[] states;
```

这个 `states` 本质上只是 `string[]`，但 `[ClassTypeName(typeof(PlayerState))]` 给它加了一个自定义 Inspector 下拉菜单。

## 2. 编辑器下拉菜单收集状态类型

在编辑器里，相关逻辑位于：

```text
Assets/PLAYER TWO/Platformer Project/Scripts/Tools/Editor/ClassTypeNameDrawer.cs:37
```

它会扫描当前项目程序集里的类型：

```csharp
var classes = System.AppDomain.CurrentDomain.GetAssemblies()
    .SelectMany(assembly => assembly.GetTypes())
    .Where(type => type.IsSubclassOf(m_classTypeName.type));
```

如果是玩家状态，它就找所有继承自 `PlayerState` 的类，比如：

```csharp
IdlePlayerState
WalkPlayerState
FallPlayerState
SpinPlayerState
```

然后把这些类型的完整名字存进 `m_names`：

```csharp
m_names = classes
    .Select(type => type.ToString())
    .ToList();
```

用户在 Inspector 下拉里选了某个状态后，这里会把对应的完整类名写进 `states` 数组：

```csharp
property.stringValue = m_names[selected];
```

所以运行时 `states` 里面存的不是对象，而是类似这样的字符串：

```csharp
PLAYERTWO.PlatformerProject.IdlePlayerState
PLAYERTWO.PlatformerProject.WalkPlayerState
```

## 3. 运行时调用 GetStateList

游戏运行时，`EntityStateManager.Start()` 会调用：

```csharp
InitializeEntity();
InitializeStates();
```

`InitializeStates()` 里的第一句是：

```csharp
m_list = GetStateList();
```

而 `PlayerStateManager.GetStateList()` 只是转交给：

```csharp
return PlayerState.CreateListFromStringArray(states);
```

`PlayerState` 继承自 `EntityState<Player>`，所以实际调用的是 `EntityState<T>` 里的静态方法，位置在：

```text
Assets/PLAYER TWO/Platformer Project/Scripts/Entity/EntityState.cs:113
```

```csharp
public static List<EntityState<T>> CreateListFromStringArray(string[] array)
{
    var list = new List<EntityState<T>>();

    foreach (var typeName in array)
    {
        list.Add(CreateFromString(typeName));
    }

    return list;
}
```

## 4. 字符串通过反射变成状态对象

每个字符串再通过反射实例化，相关方法位于：

```text
Assets/PLAYER TWO/Platformer Project/Scripts/Entity/EntityState.cs:102
```

```csharp
public static EntityState<T> CreateFromString(string typeName)
{
    return (EntityState<T>)System.Activator
        .CreateInstance(System.Type.GetType(typeName));
}
```

也就是：

```text
字符串类名 -> System.Type.GetType(typeName) -> Activator.CreateInstance(...) -> 状态对象
```

## 5. InitializeStates 缓存状态列表和字典

最后回到 `InitializeStates()`，它拿到这个 `m_list` 后，再缓存到字典 `m_states` 里。相关位置在：

```text
Assets/PLAYER TWO/Platformer Project/Scripts/Entity/EntityStateManager.cs:77
```

```csharp
foreach (var state in m_list)
{
    var type = state.GetType();

    if (!m_states.ContainsKey(type))
    {
        m_states.Add(type, state);
    }
}
```

所以：

- `m_list`：按 Inspector 配置顺序保存状态对象，用于 `Change(int index)` 和初始状态。
- `m_states`：按状态类型保存状态对象，用于 `Change<SomeState>()` 快速查找。
- `current = m_list[0]`：把 Inspector 配置里的第一个状态设为默认当前状态。

## 总结

整个流程可以概括为：

```text
Inspector 下拉选择状态
-> states 字符串数组保存状态完整类名
-> GetStateList()
-> CreateListFromStringArray(states)
-> CreateFromString(typeName)
-> Type.GetType(typeName)
-> Activator.CreateInstance(...)
-> 得到 EntityState 对象
-> 存入 m_list 和 m_states
```
