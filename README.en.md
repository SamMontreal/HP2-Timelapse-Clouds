# HP2 Timelapse Clouds

Regional volumetric clouds for ZloiKILLER's Windows port of the PS2 edition of Need for Speed: Hot Pursuit 2. The default mode plays 20 seconds of cloud motion in five seconds. Clear daytime tracks feature cumulus with shaded bases, bright sun-facing edges and a thin upper layer. Ten regional profiles control cloud distribution and lighting. The sun and its halo dim as dense clouds cross them.

## Install and play

1. Install version 0.14 of the [Windows port](https://github.com/ZloiKILLER/nfshp2-port-win) using your own supported game disc. The mod requires 64-bit Windows and a Vulkan-capable graphics card.
2. Close the game. Extract this ZIP into the folder containing speed.exe and the data directory. After extraction, Play-Volumetric-Clouds.cmd should be next to speed.exe.
3. Run Play-Volumetric-Clouds.cmd for 4× clouds. In the Timelapse-Clouds folder, Play-Clouds-1x.cmd launches normal-speed clouds, and Play-Original-Sky.cmd launches the original sky with the mod profile. Restart the game to switch modes.

## Settings and saves

On first launch, the mod copies hp2.ini from the game root if it exists. Mod settings and saves live in Timelapse-Clouds. To migrate from the test version, copy hp2.ini and the saves folder from Clouds-Test to Timelapse-Clouds while the game is closed. If you have played both versions, back up both saves folders before merging.

To return to the standard port, launch the original speed.exe in the game root. To remove the mod, first back up the settings and saves you wish to keep, then remove Timelapse-Clouds and Play-Volumetric-Clouds.cmd. The project provides one Windows download. You can read the installation guide in English or Simplified Chinese.

## Build

The renderer patch targets ZloiKILLER/nfshp2-port-win at commit da838179be46821eb10c306326504320850733fc. The coverage generator reads ten sky texture pairs from the installed game. The generator packs periodic 3D density and erosion into the remaining channels of the weather texture. Version 1.2.2 gives cumulus lobes independent growth, lift and dissipation. Their evolving roof, base and thickness are blended once per picture. Shading, sun visibility, the main camera, mirrors and reflections use the same geometry. Other weather profiles have rising masses with separately moving fine erosion. Short sampling intervals are retained for grazing views. Version 1.3.0 adds single-scattered sunlight in the air below cloud banks and inside visible gaps. A per-frame solar transmittance map follows the evolving cloud density. Both cloud speeds enable shafts by default. To compare, set HP2_CLOUD_SHAFTS=0 in the shell that starts the launcher. The default load comparison uses 3840×2160 at 100% render scale, SMAA 4, 16× anisotropic filtering and high reflections, at a fixed coastal grid position and on a forest replay.

In a Windows developer shell with Python 3.11+, Git, Clang, CMake, Ninja and the Vulkan SDK, run these commands from this mod repository. Replace GAME_FOLDER with the path to the extracted game, including the original track archives in data/TRACKS.

```powershell
git clone https://github.com/ZloiKILLER/nfshp2-port-win upstream
git -C upstream checkout da838179be46821eb10c306326504320850733fc
git -C upstream apply --ignore-space-change ../timelapse.patch
python -m pip install numpy Pillow
python prepare_weather.py "GAME_FOLDER" upstream
```

Follow [the upstream build notes](https://github.com/ZloiKILLER/nfshp2-port-win/blob/da838179be46821eb10c306326504320850733fc/TECHNICAL.md) to generate the CPU and audio code from your disc. From the mod directory, configure and build the patched runtime:

```powershell
cmake -S upstream/recomp -B upstream/build -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DHP2_NAME=speed-clouds
cmake --build upstream/build --target hp2recomp
```

The three launchers share one runtime. They select cloud speed with HP2_CLOUD_SPEED (1 or 4) and use HP2_CLOUDS=0 for the original sky. To package the runtime:

```powershell
python package.py upstream/recompwin/speed-clouds.exe
```

## Credits

Cloud modification published by SamMontreal. Windows port and native Vulkan renderer by ZloiKILLER. Upstream credits and third-party notices are listed in TECHNICAL.md in the upstream repository. Need for Speed and Hot Pursuit are trademarks of Electronic Arts.
