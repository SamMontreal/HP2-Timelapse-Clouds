# HP2 Mod Updater

The Windows launcher updates HP2 ShiftLight and Timelapse Clouds for the 0.14 recompilation. It includes verified copies of both mods for an offline first install. Extract the whole bundle, open `HP2-UpdateAndPlay.exe` and select the game directory if prompted. Subsequent launches can use the copy of the launcher in that directory. [Download the Windows bundle](https://github.com/SamMontreal/HP2-Timelapse-Clouds/archive/refs/heads/auto-update.zip)

The default action checks both mods, applies changes and starts the existing joint game launcher. `--update-only` installs without starting a race. `--offline` uses verified bundled or cached packages. `--game-dir` selects an installation. An unattended check can use `--quiet --update-only --game-dir "F:\Games\NFSHP2-Recomp"`. Exit codes are 0 for success, 1 for a failure, 2 for an active game or updater, and 3 for cancellation. The last completed check is logged under `HP2-ModUpdates` in the game directory.

## Publishing

Run `updater\build.cmd` on x64 Windows with .NET Framework 4.8. Run `python updater/package.py` with Python 3.11 or later to build the distribution under `dist/updater`. The packager can read existing Windows archives or fetch the pinned copies in `bundled.json`.

For each mod, publish a stable `windows-vMAJOR.MINOR.PATCH` branch containing the normal Windows ZIP and its `SHA256SUMS.txt`. The launcher selects the highest numeric stable version separately for each repository. Metadata and package downloads use the same immutable commit. New mod versions become discoverable as soon as that branch is published. [GitHub matching references](https://docs.github.com/en/rest/git/refs#list-matching-references)

To publish a launcher or refresh the offline bundle, publish the six files generated under `dist/updater/bundle`, at the same relative paths, to this repository's `auto-update` branch. The GitHub branch ZIP is the stable download. For a launcher code change, raise its assembly and file version before building. The launcher verifies newer workers using `launcher.json`, stores them side by side and selects the verified worker on the following launch. Update the pinned versions and digests in `bundled.json` when refreshing the offline seed packages.

The updater installs only the named mod payloads. It verifies candidate packages and engine metadata before committing a batch, backs up changed files, and recovers an interrupted commit on the next launch. A running game or HUD defers installation.
