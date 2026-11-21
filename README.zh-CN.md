# HP2 Timelapse Clouds

《极品飞车：热力追踪 2》PS2 重编译 Windows 版的动态体积云模组。三组白天赛道使用有冷色云底、向阳亮边的积云。默认 4 倍速，实际 5 秒可看到原速 20 秒的云团漂移、生长和消散。十组地区配置控制云层分布和光照。日轮及光晕随云层透光率变化，后视镜和环境反射使用同一云层时钟。

适配重编译程序 0.14。地区覆盖来自原版赛道纹理，云团体积噪声在构建时烘焙。1.2.2 让积云内的云瓣各自生长、抬升和消散，云底和云顶随时间改变。云层侧面的细节沿另一股气流演变，日落和阴天的云体也会随气流抬升。每帧只混合一次云瓣的高度和厚度，主画面、后视镜、车身反射和太阳遮挡使用同一份数据。低角度的云仍使用短步距采样。

1.3.0 在云底和可见的云隙中加入体积光。光照沿太阳方向穿过当前云团，云隙下方的空气被照亮，厚云背后的光束减弱。4 倍速和原速均默认开启。若要比较，在命令提示符中执行 `set HP2_CLOUD_SHAFTS=0`，再从同一窗口运行启动器。

默认负载对比使用 3840×2160 全屏、100% 渲染比例、SMAA 4、16× 各向异性过滤和高环境反射，在同一沿海发车位和森林回放段记录整帧 GPU 耗时。

沿海黄昏赛道加入成团的积云，保留粉灰色天空。迎光的一侧偏暖，云底偏冷，云形随风生长和消散。

## 安装与启动

1. 准备已经从自有游戏光盘安装好的 [ZloiKILLER Windows 移植版](https://github.com/ZloiKILLER/nfshp2-port-win)。需要 Windows x64 和 Vulkan 显卡。
2. 将 Windows ZIP 解压到含有 speed.exe 和 data 文件夹的游戏根目录。
3. 双击游戏根目录的 **Play-Volumetric-Clouds.cmd**。原速使用 Timelapse-Clouds/Play-Clouds-1x.cmd，原版天空使用 Timelapse-Clouds/Play-Original-Sky.cmd。

启动器首次运行时会从游戏根目录复制已有的 hp2.ini。模组使用 Timelapse-Clouds 下的配置和存档。旧测试版用户可以在退出游戏后将 Clouds-Test 下的 hp2.ini 和 saves 复制到 Timelapse-Clouds，再次启动即可接续进度。原移植版仍可使用 speed.exe 启动。

## 切换与移除

退出游戏后再切换入口。旧测试版和正式版若都有游玩进度，先分别备份两边的 saves，再迁移所需存档。要恢复标准移植版，运行游戏根目录的 speed.exe。要移除模组，先备份需要保留的存档和 hp2.ini，再移走 Timelapse-Clouds 文件夹和 Play-Volumetric-Clouds.cmd。体积云统一发布一个 Windows 安装包，安装说明提供中文和英文两个版本。

## 分地区的天空

模组按原版赛道的天空贴图识别天气。在 30 份赛道纹理包中，五个地区各有两组天空。表中编号是游戏的资源编号，同一栏的三个编号共用一套天空素材。

| 地区 | 第一组天空 | 第二组天空 |
| --- | --- | --- |
| 海岸公园和国家森林 | 11–13 · 1.3.0 的白天积云 | 14–16 · 1.3.0 的林地云层 |
| 沙漠与峡谷 | 21–23 · 1.3.0 的第一组沙漠天空 | 24–26 · 1.3.0 的第二组沙漠天空 |
| 地中海海岸与遗迹 | 31–33 · 蓝天与边缘明亮的海上积云 | 34–36 · 动态积云，受光面橙红，近太阳薄边呈金色 |
| 秋季山地与高山 | 41–43 · 通透的白天天空，云缘清晰、随气流变形的薄云 | 44–46 · 紫灰色中层云带，低太阳照亮云带边缘与部分云底 |
| 热带海岛 | 61–63 · 潮湿地平线、冷色云底的海岛积云 | 64–66 · 低处受金色阳光照亮的积云，云间露出蓝天 |

原版 1.3.0 的 11–13、31–33、61–63 使用动态积云，其余七组共用另一种体密度函数。1.4.0 根据各组原版全景的轮廓、配色和太阳方向分别处理。黄昏时，太阳高度和云内遮光控制暖色强度，远离太阳或被遮挡的云保留较冷的阴影。云隙光束沿游戏太阳的方向出现。

气象参考：[世界气象组织《国际云图集》云的颜色](https://cloudatlas.wmo.int/en/appearance-of-clouds-colour.html) · [NASA 关于低角度太阳与云隙光的实拍说明](https://science.nasa.gov/earth/earth-observatory/crepuscular-rays-and-light-scattering-150090/)

1.4.2 在七组非黄昏环境恢复了 1.3.0 的天空，包括 41–43 号秋季山地。34–36、44–46、64–66 号保留黄昏的定向受光。路面、护栏和山坡会随云遮日变暗。地面原有烘焙光照保留为底色。设置 HP2_CLOUD_GROUND=0 可对比原有地面光照。HP2_CLOUD_SHAFTS=0 关闭天空光束。

1.4.3 调整 41–43 号秋山白天的薄云。云块之间留出空隙，受光边缘更清晰，细碎边缘和云身按不同速度变形。比赛主视角的不透明道路与适用场景材质从原始纹理取底色，按三角面朝向、太阳方向和实时云层遮挡计算直射光。原图中画入的阴影仍可能可见。日轮与光晕读取相机位置的云层透光率。设置 HP2_CLOUD_GROUND=0 可切回原版地面着色。

## 赛道光照

1.4.8 恢复了路面的定向日照。沥青和铺石路以原版贴图及顶点明暗为底色，再按坡面朝向和实时云层透光率受光。原版烘焙阴影和车辆投影均保留，海岛天空沿用 1.4.4。HP2_CLOUD_SHAFTS 控制天空光束，HP2_CLOUD_GROUND 独立控制地面动态照明。进入有警力的赛事时，体积云程序会自动启动游戏根目录中已安装的 HP2-ShiftLight.exe。

默认使用原版轮胎烟雾绘制。实验合成器可通过 HP2_EXPERIMENTAL_SMOKE=1 单独启用。

## 黄昏场景性能

RTX 2080 SUPER，3840×2160，100% 渲染比例。固定同一车辆和 34 号赛道正向发车位，关闭截帧，预热后对比 24 个一秒区间的 GPU 平均帧耗时中位数。原版天空 4.20 毫秒，四倍速体积云 1.3.0 为 5.71 毫秒，1.4.0 约为 6.55 毫秒。

## 从源码构建

使用 Windows 开发者命令行，准备 Python 3.11 或更新版本、Git、Clang、CMake、Ninja 和 Vulkan SDK。在模组仓库根目录运行下列命令。将 GAME_FOLDER 替换为已安装游戏的路径，其中应有原始 data/TRACKS 赛道纹理包。

```powershell
git clone https://github.com/ZloiKILLER/nfshp2-port-win upstream
git -C upstream checkout da838179be46821eb10c306326504320850733fc
git -C upstream apply --ignore-space-change ../timelapse.patch
python -m pip install numpy Pillow
python prepare_weather.py "GAME_FOLDER" upstream
```

按[上游构建说明](https://github.com/ZloiKILLER/nfshp2-port-win/blob/da838179be46821eb10c306326504320850733fc/TECHNICAL.md)从自己的光盘生成 CPU 和音频代码，再从模组目录配置并编译。

```powershell
cmake -S upstream/recomp -B upstream/build -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DHP2_NAME=speed-clouds
cmake --build upstream/build --target hp2recomp
```

三个入口共用 speed-clouds.exe。HP2_CLOUD_SPEED 选择 1 倍或 4 倍云速，HP2_CLOUDS=0 启用原版天空。打包命令如下。

```powershell
python package.py upstream/recompwin/speed-clouds.exe
```

同一张 RTX 2080 SUPER、4K、100% 渲染比例、四倍云速下，对比 1.4.1 与 1.4.2。34 号赛道 GPU 帧耗时为 6.205 → 6.515 毫秒，61 号赛道为 10.415 → 10.555 毫秒。测量时关闭截图，取预热后多个一秒平均值的中位数。

## 制作人员

体积云模组由 SamMontreal 发布。Windows 移植及原生 Vulkan 渲染器由 ZloiKILLER 制作。第三方组件说明见上游 TECHNICAL.md。Need for Speed 和 Hot Pursuit 是 Electronic Arts 的商标。
