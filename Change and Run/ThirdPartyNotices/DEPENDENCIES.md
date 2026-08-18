# Third-Party Dependencies

Versions in this file must match `Packages/manifest.json` and
`Packages/packages-lock.json`. Package upgrades require a separate review.

| Dependency | Locked version | Source | License | Purpose | Added |
|---|---|---|---|---|---|
| Unity Input System | 1.7.0 | Unity Registry | Unity Companion License | Runtime input abstraction | 2026-08-17 |
| Unity Localization | 1.5.3 | Unity China Registry | Unity Companion License | Locale and String Table support | 2026-08-17 |
| Unity Test Framework | 1.1.33 | Unity Registry | Unity Companion License | EditMode and PlayMode tests | 2026-08-15 |
| Newtonsoft Json for Unity | 3.2.1 | Unity Registry | MIT | Versioned JSON serialization | 2026-08-17 |
| Steamworks.NET | 2024.8.0 (`a2fc889`) | [Official repository](https://github.com/rlabrecque/Steamworks.NET/tree/2024.8.0) | MIT | Steamworks C# wrapper | 2026-08-17 |

Unity Registry packages include their license files in the resolved package
cache. Steamworks.NET's MIT license is archived in `Licenses/Steamworks.NET.txt`.

Transitive dependencies are locked by Unity in `Packages/packages-lock.json`.
They must be reviewed again after Unity resolves the updated manifest.
