# CTBans
Often used with [Jailbreak](https://github.com/playpark/cs2-jailbreak)

![CS2](https://img.shields.io/badge/Game-CS2-orange)
![CounterStrikeSharp](https://img.shields.io/badge/API-CounterStrikeSharp-blue)


## Features

- Ban players from joining the CT team for a specified duration
- Offline banning capability
- Ban tracking with time served (only counts time when player is alive)
- Admin commands for managing bans
- Player commands to check ban status
- HUD notifications for banned players
- Multi-language support
- API for other plugins to integrate with

## Configuration

The plugin configuration file is located at `configs/plugins/CTBans/CTBans.json`. Here's an example configuration:

```json
{
  "ConfigVersion": 2,
  "Debug": false,
  "Database": {
    "Table": "ctbans",
    "Host": "localhost",
    "Username": "user",
    "Password": "password",
    "Name": "database",
    "Port": 3306
  },
  "Commands": {
    "Permission": "@css/ban",
    "CTBan": "ctban,banct",
    "CTUnban": "ctunban,unctban,unbanct",
    "CTBanInfo": "ctbaninfo,infoctban,ctbancheck,checkctban,isctban,isctbanned",
    "AddCTBan": "addctban,offlinebanct"
  },
  "TeamDenySound": "sounds/ui/counter_beep.vsnd"
}
```

### Configuration Options

- `ConfigVersion`: Current configuration version (do not change)
- `Debug`: Enable debug mode for troubleshooting
- `Database`: MySQL database connection settings
  - `Table`: Database table name for storing bans
  - `Host`: MySQL server hostname
  - `Username`: MySQL username
  - `Password`: MySQL password
  - `Name`: Database name
  - `Port`: MySQL server port
- `Commands`: Command aliases configuration
  - `Permission`: Permission flag required to use admin commands
  - `CTBan`: Command aliases for banning players
  - `CTUnban`: Command aliases for unbanning players
  - `CTBanInfo`: Command aliases for checking ban information
  - `AddCTBan`: Command aliases for offline banning
- `TeamDenySound`: Sound played when a banned player attempts to join CT

## Commands

### Admin Commands

| Command | Description | Usage |
|---------|-------------|-------|
| `css_ctban` | Ban a player from CT side | `css_ctban <name/steamid> <duration in minutes> <reason>` |
| `css_ctunban` | Unban a player from CT side | `css_ctunban <name/steamid>` |
| `css_ctbaninfo` | Check a player's CT ban info | `css_ctbaninfo <name/steamid>` |
| `css_addctban` | Ban an offline player from CT side | `css_addctban <steamid> <name> <duration in minutes> <reason>` |

### Player Commands

| Command | Description | Usage |
|---------|-------------|-------|
| `css_checkban` | Check your own or another player's CT ban status | `css_checkban [name/steamid]` |


## Localization

The plugin supports multiple languages. Language files are located in the `lang` directory. To add a new language, create a new JSON file with the language code (e.g., `fr.json`) and translate the strings from the `en.json` file.

## Credits

- Original author: DeadSwim
- Continued by: exkludera
- Current maintainer: dollan


