

## 功能

- 动态加载mod

- 动态修改mod代码

## 使用方法

1. 将要热重载的mod放入`游戏路径\hollow_knight_Data\HKDebug\HotReloadMods`目录下
2. 在游戏中按下`F5`加载mod

## 注意

- 热重载器会拒绝处理所有泛型类型

- 所有被热重载器加载的mod都使用`Assembly.Load(byte[])`加载

- 如果热重载器找不到与旧方法签名相同的新方法，尝试调用旧方法时将会抛出`HRBadMethodException`

- 在字段上使用`HotReloadIgnoreAttribute`可以使热重载器在创造类型副本时不会自动转换该字段并为该字段赋值，可以在`void OnAfterHotReload(Dictionary<string,object> data)`中手动赋值

- 在方法上使用`HotReloadIgnoreAttribute`可以使热重载器拒绝重定向该方法

- 在类型上使用`HotReloadIgnoreAttribute`可以使热重载器忽略该类型

- 新的`Unity组件`会被添加到旧的`Unity组件`所在的`GameObject`上，会正常调用`Awake` `Start` `OnEnable`等方法

- `Unity组件`中的`Update` `FixedUpdate` 等方法可能在同一帧中会被调用多次，添加`EmptyMethodAttribute`以确保`Unity`对旧方法的调用不会传递到新方法中

- 修改旧对象中的字段不会映射到新对象中，反之亦然

  