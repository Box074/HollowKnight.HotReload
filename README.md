## Function

- Load mods dynamically

- Modify mod code dynamically

## Instructions

1. Put the mod to be hot reloaded into the `Game Path\hollow_knight_Data\HKDebug\HotReloadMods` directory
2. Press `F5` in the game to load the mod

## Notice

- The hot reloader will refuse to handle all generic types

- All mods loaded by the hot reloader are loaded with `Assembly.Load(byte[])`

- If the hot reloader cannot find a new method with the same signature as the old method, it will throw a `HRBadMethodException` when trying to call the old method

- Using `HotReloadIgnoreAttribute` on a field can make the hot reloader not automatically convert the field and assign a value to the field when creating a copy of the type. You can manually assign a value in `void OnAfterHotReload(Dictionary<string,object> data)`

- Using `HotReloadIgnoreAttribute` on the method can make the hot reloader refuse to redirect the method

- Use `HotReloadIgnoreAttribute` on the type to make the hot reloader ignore the type

- The new `Unity component` will be added to the `GameObject` where the old `Unity component` is located, and methods such as `Awake`, `Start`, and `OnEnable` will be called normally

- Methods such as `Update` and `FixedUpdate` in `Unity component` may be called multiple times in the same frame. Add `EmptyMethodAttribute` to ensure that calls made by `Unity` to the old method will not be passed to the new method

- Modifying the fields in the old object will not be mapped to the new object, and vice versa