# Third-party notices / 第三方组件声明

The all-in-one package (`UltraMode-v<version>-with-BepInEx.zip`) contains the official, unmodified
**BepInEx 6.0.0-be.788+5b766a3** build for Unity IL2CPP win-x64, downloaded from
https://builds.bepinex.dev/projects/bepinex_be and checked against the SHA-256 in UltraMode.csproj.
Its components are listed below with their licenses and the source code of the exact version shipped.
None of them were modified. Ultra Mode itself only uses BepInEx's public API at run time.

整合包（`UltraMode-v<版本>-with-BepInEx.zip`）原样包含官方 **BepInEx 6.0.0-be.788+5b766a3**（Unity IL2CPP win-x64），
未做任何修改。下表列出其中各组件的许可证，以及对应版本的源码地址。

| Component | Version | License | Files | Source | License text |
| --- | --- | --- | --- | --- | --- |
| BepInEx | 6.0.0-be.788 | LGPL-2.1 | `BepInEx/core/BepInEx.*.dll` | [source](https://github.com/BepInEx/BepInEx/tree/5b766a3b7f6c164d4798924a93f3acf4db769d06) | [BepInEx.txt](licenses/BepInEx.txt) |
| UnityDoorstop | 4.5.0 | LGPL-2.1 | `winhttp.dll, doorstop_config.ini` | [source](https://github.com/NeighTools/UnityDoorstop/tree/v4.5.0) | [UnityDoorstop.txt](licenses/UnityDoorstop.txt) |
| Il2CppInterop | 1.5.3 | LGPL-3.0 | `BepInEx/core/Il2CppInterop.*.dll` | [source](https://github.com/BepInEx/Il2CppInterop/tree/dbda1cb353b0f4253345dc45136d170b9e50a5a0) | [Il2CppInterop.txt](licenses/Il2CppInterop.txt) |
| HarmonyX | 2.10.2 | MIT | `BepInEx/core/0Harmony.dll` | [source](https://github.com/BepInEx/HarmonyX/tree/v2.10.2) | [HarmonyX.txt](licenses/HarmonyX.txt) |
| MonoMod | 22.07.31.01 / Backports 1.1.2 | MIT | `BepInEx/core/MonoMod.*.dll` | [source](https://github.com/MonoMod/MonoMod/tree/v22.07.31.01) | [MonoMod.txt](licenses/MonoMod.txt) |
| Cpp2IL | 2022.1.0-development.1452 | MIT | `BepInEx/core/Cpp2IL.Core.dll, LibCpp2IL.dll, StableNameDotNet.dll, WasmDisassembler.dll` | [source](https://github.com/SamboyCoding/Cpp2IL/tree/558ddd98642010897d54316b51fbaa7889fda093) | [Cpp2IL.txt](licenses/Cpp2IL.txt) |
| Disarm | 2022.1.0-master.99 | MIT | `BepInEx/core/Disarm.dll` | [source](https://github.com/SamboyCoding/Disarm/tree/8574010015c8c56bb7faad7e407c50b0abe9cc72) | [Disarm.txt](licenses/Disarm.txt) |
| AsmResolver | 6.0.0-beta.5 | MIT | `BepInEx/core/AsmResolver*.dll` | [source](https://github.com/Washi1337/AsmResolver/tree/124e161a9bc0ebb83b4d97276ae3da3c9cfe2fc5) | [AsmResolver.txt](licenses/AsmResolver.txt) |
| AssetRipper.CIL | 1.2.2 | MIT | `BepInEx/core/AssetRipper.CIL.dll` | [source](https://github.com/AssetRipper/AssetRipper.CIL/tree/06216e69825ccd6536dc17e9ca81c4d539ce9321) | [AssetRipper.CIL.txt](licenses/AssetRipper.CIL.txt) |
| AssetRipper.Primitives | 3.2.0 | MIT | `BepInEx/core/AssetRipper.Primitives.dll` | [source](https://github.com/AssetRipper/AssetRipper.Primitives/tree/d759b3db98bf2a1cb8c2181faf5d405610aac585) | [AssetRipper.Primitives.txt](licenses/AssetRipper.Primitives.txt) |
| Mono.Cecil | 0.11.4 | MIT | `BepInEx/core/Mono.Cecil*.dll` | [source](https://github.com/jbevain/cecil/tree/0.11.4) | [Mono.Cecil.txt](licenses/Mono.Cecil.txt) |
| Iced | 1.21.0 | MIT | `BepInEx/core/Iced.dll` | [source](https://github.com/icedland/iced/tree/601c40570d6c986c8e1a6a2e483dd2fdf5ade552) | [Iced.txt](licenses/Iced.txt) |
| Capstone.NET | 2.3.2 | MIT | `BepInEx/core/Gee.External.Capstone.dll` | [source](https://github.com/9ee1/Capstone.NET/tree/b90e380c14e857865c94a355954236e678bd557c) | [Capstone.NET.txt](licenses/Capstone.NET.txt) |
| SemanticVersioning | 2.0.2 | MIT | `BepInEx/core/SemanticVersioning.dll` | [source](https://github.com/adamreeve/semver.net) | [SemanticVersioning.txt](licenses/SemanticVersioning.txt) |
| Dobby | BepInEx fork | Apache-2.0 | `BepInEx/core/dobby.dll` | [source](https://github.com/BepInEx/Dobby) | [Dobby.txt](licenses/Dobby.txt) |
| .NET Runtime | 6.0.7 | MIT | `dotnet/` | [source](https://github.com/dotnet/runtime/tree/v6.0.7) | [dotnet-runtime.txt](licenses/dotnet-runtime.txt) |

LGPL-3.0 supplements GPL-3.0; Il2CppInterop's license file contains both texts.
The .NET runtime's own third-party notices: [dotnet-runtime-THIRD-PARTY-NOTICES.txt](licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt).

Under the LGPL you may replace these libraries with your own builds: every file listed above is a separate file
that can be swapped in place, and BepInEx loads whatever is there.