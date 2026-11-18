# HP2 Timelapse Clouds

《极品飞车：热力追踪 2》PS2 重编译 Windows 版的动态体积云模组。正式版使用 v0.3.2 云形。默认 4 倍速，实际 5 秒可看到原速 20 秒的云团漂移、生长和消散。十组地区配置控制云层分布和光照。日轮及光晕随云层透光率变化，后视镜和环境反射使用同一云层时钟。

## 安装与启动

1. 准备已经从自有游戏光盘安装好的 [ZloiKILLER Windows 移植版](https://github.com/ZloiKILLER/nfshp2-port-win)。需要 Windows x64 和 Vulkan 显卡。
2. 从 [Releases](https://github.com/SamMontreal/HP2-Timelapse-Clouds/releases/latest) 下载 Windows ZIP，解压到含有 speed.exe 和 data 文件夹的游戏根目录。
3. 双击游戏根目录的 **Play-Volumetric-Clouds.cmd**。原速使用 Timelapse-Clouds/Play-Clouds-1x.cmd，原版天空使用 Timelapse-Clouds/Play-Original-Sky.cmd。

启动器首次运行时会从游戏根目录复制已有的 hp2.ini。模组使用 Timelapse-Clouds 下的配置和存档。旧测试版用户可以在退出游戏后将 Clouds-Test 下的 hp2.ini 和 saves 复制到 Timelapse-Clouds，再次启动即可接续进度。原移植版仍可使用 speed.exe 启动。

## 切换与移除

退出游戏后再切换入口。旧测试版和正式版若都有游玩进度，先分别备份两边的 saves，再迁移所需存档。要恢复标准移植版，运行游戏根目录的 speed.exe。要移除模组，先备份需要保留的存档和 hp2.ini，再移走 Timelapse-Clouds 文件夹和 Play-Volumetric-Clouds.cmd。体积云统一发布一个 Windows 安装包，安装说明提供中文和英文两个版本。

## 从源码构建

使用 Windows 开发者命令行，准备 Python 3.11 或更新版本、Git、Clang、CMake、Ninja 和 Vulkan SDK。在模组仓库根目录运行下列命令。将 GAME_FOLDER 替换为已安装游戏的路径，其中应有原始 data/textures/index.csv 及对应 PNG。

```powershell
git clone https://github.com/ZloiKILLER/nfshp2-port-win upstream
git -C upstream checkout 8ae05937be6865dde1e45c108cecd79f247d6b7a
git -C upstream apply --ignore-space-change ../timelapse.patch
python -m pip install numpy Pillow
python prepare_weather.py "GAME_FOLDER" upstream
```

按[上游构建说明](https://github.com/ZloiKILLER/nfshp2-port-win/blob/8ae05937be6865dde1e45c108cecd79f247d6b7a/TECHNICAL.md)从自己的光盘生成 CPU 和音频代码，再从模组目录配置并编译。

```powershell
cmake -S upstream/recomp -B upstream/build -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DHP2_NAME=speed-clouds-lapse-4x
cmake --build upstream/build --target hp2recomp
```

要构建原速版，将 upstream/recomp/runtime/native_vk.cpp 中的 cloudTimeScale 改为 1.0f，在单独的构建目录配置 -DHP2_NAME=speed-clouds-flow-032 并编译。完成后将源码倍率恢复为 4.0f。用下列命令打包两个程序。

```powershell
python package.py upstream/recompwin/speed-clouds-lapse-4x.exe upstream/recompwin/speed-clouds-flow-032.exe
```

## 制作人员

体积云模组由 SamMontreal 发布。Windows 移植及原生 Vulkan 渲染器由 ZloiKILLER 制作。第三方组件说明见上游 TECHNICAL.md。Need for Speed 和 Hot Pursuit 是 Electronic Arts 的商标。
