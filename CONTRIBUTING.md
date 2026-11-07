# 参与贡献 / Contributing

[English](#english) | 简体中文

欢迎提交问题和代码。

## 报告问题

请附上：

- `BepInEx/LogOutput.log`（完整文件）；
- 游戏版本、BepInEx 版本、本 mod 版本，以及同时安装的其他 mod；
- 复现步骤。如果是动作或魔导器相关的问题，打开 `DebugLogging` 复现一次，再按 F11 附上 `BepInEx/ultra_mode_dump/` 里的文件。

## 提交代码

1. 先读 [docs/](docs/README.md)，特别是[架构](docs/architecture.md)和[开发环境与调试](docs/development.md)。
2. 一个 PR 只做一件事。新功能请先开 issue 讨论，确认方向后再写。
3. 新功能按[新增一个功能](docs/adding-a-feature.md)的步骤，同时更新两份 README 和 `docs/features.md`。
4. 提交前：`dotnet build -c Release` 无警告，并按[测试清单](docs/testing.md)在游戏里验证改动涉及的功能。
5. 遵循现有代码风格：英文代码和注释，注释只写代码看不出来的原因和约束。

**不要提交**任何游戏文件、反编译代码（`reference/`）或从游戏导出的数据（`tools/*.txt`）。

## 许可

除非另有声明，你提交的贡献将按与本项目相同的条款授权：MIT 或木兰宽松许可证第 2 版，由使用者任选其一。

---

## English

Issues and pull requests are welcome.

**Reporting a problem:** attach the full `BepInEx/LogOutput.log`; the game, BepInEx and mod versions and any other mods installed; and steps to reproduce. For action or relic issues, reproduce once with `DebugLogging` on, then press F11 and attach the files in `BepInEx/ultra_mode_dump/`.

**Contributing code:**

1. Read [docs/](docs/README.md) first (in Chinese), especially the architecture and development pages.
2. One change per pull request. Please open an issue before starting on a new feature.
3. For a new feature, follow `docs/adding-a-feature.md` and update both READMEs and `docs/features.md`.
4. Before submitting: `dotnet build -c Release` with no warnings, and test the affected features in game using `docs/testing.md`.
5. Match the existing style: code and comments in English; comments explain constraints and reasons the code can't show.

**Never commit** game files, decompiled code (`reference/`) or data exported from the game (`tools/*.txt`).

**License:** unless you state otherwise, contributions are licensed under the same terms as the project: MIT OR Mulan PSL v2, at the recipient's choice.
