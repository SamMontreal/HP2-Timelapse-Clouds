# HP2 Timelapse Clouds

Regional volumetric clouds for ZloiKILLER's Windows port of the PS2 edition of Need for Speed: Hot Pursuit 2. The default mode plays 20 seconds of cloud motion in five seconds. Clear daytime tracks feature cumulus with shaded bases and bright sun-facing edges. On the Mediterranean sunset tracks, distinct cumulus banks catch peach light above a shaded base. Ten regional profiles control cloud distribution and lighting. The sun and its halo dim as dense clouds cross them.

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

## Regional skies

Thirty track texture archives resolve to ten sky profiles. The original panorama, sky cap and the projected in-game sun guide each profile.

| Environment | First sky | Second sky |
| --- | --- | --- |
| Parkland | 11–13 · 1.3.0 daytime cumulus | 14–16 · 1.3.0 regional cloud cover |
| Desert | 21–23 · 1.3.0 first desert sky | 24–26 · 1.3.0 second desert sky |
| Mediterranean | 31–33 · maritime cumulus against blue sky | 34–36 · rolling cumulus banks, orange sunlit faces and gold edges |
| Alpine | 41–43 · thin autumn clouds with distinct, evolving sunlit edges | 44–46 · a purple-grey mid-level bank and warm, illuminated edges |
| Tropical | 61–63 · low trade cumulus and the original distant island sky | 64–66 · gold-lit island cumulus with blue sky between banks |

Version 1.4.2 restores the sky rendering from 1.3.0 on the seven non-sunset profiles, including autumn mountain tracks 41–43. The Mediterranean, Alpine and tropical sunset profiles retain their directional lighting. Cloud transmittance now shades the baked light on opaque race geometry. Light scattered through nearby cloud gaps is integrated up to the visible surface using its depth. Set HP2_CLOUD_GROUND=0 to compare with the original surface lighting. HP2_CLOUD_SHAFTS=0 also turns off near-ground scattering while retaining surface cloud shadows.

Version 1.4.3 gives the thin clouds on Alpine daytime tracks 41–43 clearer sunlit edges and faster deformation within each cloud. Opaque textured road and track surfaces in the main race view now use real-time diffuse sunlight from the surface normal, the game sun and the current cloud transmission. The original diffuse texture supplies the material colour. Shading painted directly into the texture can remain visible. The sun disc and flare use the cloud shadow at the camera. HP2_CLOUD_GROUND=0 restores the original surface shading.

Atmospheric references: [WMO International Cloud Atlas](https://cloudatlas.wmo.int/en/appearance-of-clouds-colour.html) · [NASA Earth Observatory on crepuscular rays](https://science.nasa.gov/earth/earth-observatory/crepuscular-rays-and-light-scattering-150090/)

## Palm City Island daytime sky

Palm City Island and the other tracks using its daytime textures (61–63) have a shallow trade-wind cumulus layer. Its model envelope starts at 0.70 km and ends at 2.50 km. Individual clouds occupy part of that depth. The 1× horizontal advection speed is 9 m/s, and the default 4× launcher advances the weather clock four times as fast. The heights and wind speed use observations in the tropical Atlantic as a regional analogue. The original sky panorama and cap supply the distant cloud banks and clear-sky colour. Moving foreground clouds attenuate that background and contribute to the solar transmission used on the track. [Nuijens et al. (2015)](https://agupubs.onlinelibrary.wiley.com/doi/10.1002/2015MS000483) · [Vogel et al. (2022)](https://pmc.ncbi.nlm.nih.gov/articles/PMC9771822/)

## Track lighting

The 1.4.4 race renderer lights opaque roads, bridge decks and scenery using the surface orientation, the current sun and a map of sunlight passing through the moving clouds. The near-ground pass integrates illuminated air up to the first visible surface. Visible nearby geometry can block sunlight along that path. The effect varies with cloud cover, viewing direction and distance. Stock textures may still contain painted shadows. Use HP2_CLOUD_GROUND=0 to compare the original ground shading, or HP2_CLOUD_SHAFTS=0 to disable the air scattering and retain surface lighting.

The original tyre particle renderer is the default. The experimental compositor can be selected with HP2_EXPERIMENTAL_SMOKE=1.

## Mediterranean sunset performance

At 3840×2160, 100% render scale and 4× cloud motion, the RTX 2080 SUPER measured 4.20 ms per GPU frame with the original sky, 5.71 ms with 1.3.0, and 6.55 ms with 1.4.0. These are medians of 24 one-second GPU averages after warm-up at the same forward-direction starting grid on track 34. All runs used the same car with frame capture disabled.

An A/B test against 1.4.1 on the same RTX 2080 SUPER at 4K, 100% scale and 4× cloud speed measured 6.205 → 6.515 ms on track 34 and 10.415 → 10.555 ms on track 61. Values are medians of one-second GPU averages after warm-up, with capture disabled.

## Credits

Cloud modification published by SamMontreal. Windows port and native Vulkan renderer by ZloiKILLER. Upstream credits and third-party notices are listed in TECHNICAL.md in the upstream repository. Need for Speed and Hot Pursuit are trademarks of Electronic Arts.
