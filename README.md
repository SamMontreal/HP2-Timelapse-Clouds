# HP2 Timelapse Clouds

《极品飞车：热力追踪 2》PS2 重编译 Windows 版的动态体积云模组。v1.0.0 使用 v0.3.2 云形。默认 4 倍速，实际 5 秒可看到原速 20 秒的云团漂移、生长和消散。十组地区配置控制云层分布和光照。日轮及光晕随云层透光率变化，后视镜和环境反射使用同一云层时钟。

## 安装与启动

1. 准备已经从自有游戏光盘安装好的 [ZloiKILLER Windows 移植版](https://github.com/ZloiKILLER/nfshp2-port-win)。需要 Windows x64 和 Vulkan 显卡。
2. 从 [Releases](https://github.com/SamMontreal/HP2-Timelapse-Clouds/releases/latest) 下载 Windows ZIP，解压到含有 speed.exe 和 data 文件夹的游戏根目录。
3. 双击游戏根目录的 **Play-Volumetric-Clouds.cmd**。原速使用 Timelapse-Clouds/Play-Clouds-1x.cmd，原版天空使用 Timelapse-Clouds/Play-Original-Sky.cmd。

启动器首次运行时会从游戏根目录复制已有的 hp2.ini。模组使用 Timelapse-Clouds 下的配置和存档。旧测试版用户可以在退出游戏后将 Clouds-Test 下的 hp2.ini 和 saves 复制到 Timelapse-Clouds，再次启动即可接续进度。原移植版仍可使用 speed.exe 启动。

## Building from source

The renderer patch targets ZloiKILLER/nfshp2-port-win at commit 8ae05937be6865dde1e45c108cecd79f247d6b7a. The coverage generator reads ten sky texture pairs from the installed game. The release uses 48 primary ray steps and 24 for reflections.

In a Windows developer shell with Python 3.11+, Git, Clang, CMake, Ninja and the Vulkan SDK, run these commands from this mod repository. Replace GAME_FOLDER with the path to the extracted game, including the original data/textures/index.csv and PNGs.

```powershell
git clone https://github.com/ZloiKILLER/nfshp2-port-win upstream
git -C upstream checkout 8ae05937be6865dde1e45c108cecd79f247d6b7a
git -C upstream apply --ignore-space-change ../timelapse.patch
python -m pip install numpy Pillow
python prepare_weather.py "GAME_FOLDER" upstream
```

Follow [the upstream build notes](https://github.com/ZloiKILLER/nfshp2-port-win/blob/8ae05937be6865dde1e45c108cecd79f247d6b7a/TECHNICAL.md) to generate the CPU and audio code from your disc. From the mod directory, configure and build the patched runtime:

```powershell
cmake -S upstream/recomp -B upstream/build -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DHP2_NAME=speed-clouds-lapse-4x
cmake --build upstream/build --target hp2recomp
```

For a normal-speed build, change cloudTimeScale from 4.0f to 1.0f in upstream/recomp/runtime/native_vk.cpp, configure a separate build directory with -DHP2_NAME=speed-clouds-flow-032, and build. Restore 4.0f when finished. To package both executables:

```powershell
python package.py upstream/recompwin/speed-clouds-lapse-4x.exe upstream/recompwin/speed-clouds-flow-032.exe
```

## Credits

Cloud modification published by SamMontreal. Windows port and native Vulkan renderer by ZloiKILLER. Upstream credits and third-party notices are listed in TECHNICAL.md in the upstream repository. Need for Speed and Hot Pursuit are trademarks of Electronic Arts.
