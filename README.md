# TimeoutLimit

## About

Increases the timeout while connecting to a server to avoid disconnecting too early.
Default is 90 seconds and can be changed in the config.
In vanilla Steam has 30 seconds timeout and Playfab has 90 seconds timeout.

To work reliably, the mod must be installed on both client and server with the same configured timeout.
Depending on the specific timeout case, it may be enough to install it only on the server or only on the client.


## Installation

This mod requires [BepInEx](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim) \
Extract the content of `TimeoutLimit` into the `BepInEx/plugins` folder.


## Links

- [Thunderstore](https://valheim.thunderstore.io/package/MSchmoecker/TimeoutLimit/)
- [Github](https://github.com/MSchmoecker/TimeoutLimit)
- Discord: Margmas. Feel free to DM or ping me about feedback or questions, for example in the [Jötunn discord](https://discord.gg/DdUt6g7gyA)


## Development

See [contributing](https://github.com/MSchmoecker/TimeoutLimit/blob/master/CONTRIBUTING.md).


## Changelog

0.2.0
- Added patching of mod timeouts (embedded ServerSync, Jotunn and AzuAntiCheat)

0.1.0
- Release, patching of vanilla timeouts
