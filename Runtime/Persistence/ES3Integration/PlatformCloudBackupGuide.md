# Platform Cloud Backup Setup Guide

MToolKit now supports platform-native cloud backup using the built-in services provided by Steam, Android, and Apple. This approach is simpler and more reliable than custom cloud sync solutions.

## Supported Platforms

### Steam (PC)
- **Service**: Steam Auto Cloud
- **Setup**: Enable Steam Cloud in Steam settings for your game
- **Location**: Save files are automatically stored in Steam Cloud when saved to `Application.persistentDataPath`
- **Benefits**: Automatic sync across Steam installations, no additional setup required

### Android
- **Service**: Android Auto Backup
- **Setup**: Configure Auto Backup in Android settings (usually enabled by default)
- **Location**: Save files are backed up to Google Drive when stored in `Application.persistentDataPath`
- **Benefits**: Automatic backup to Google Drive, restore on new device installation

### Apple (iOS/macOS)
- **Service**: iCloud Backup
- **Setup**: Enable iCloud Backup in device settings
- **Location**: Save files are backed up to iCloud when stored in `Application.persistentDataPath`
- **Benefits**: Automatic backup to iCloud, sync across Apple devices

## Configuration

The `ES3SaveConfig` asset includes platform cloud backup settings:

- **Enable Platform Cloud Backup**: Toggle to enable/disable platform cloud backup
- **Use Platform Specific Paths**: Ensures save files are stored in locations compatible with cloud backup services

## Implementation Details

### Save File Location
When platform cloud backup is enabled, save files are stored in `Application.persistentDataPath`, which is the standard location that all platform cloud services monitor:

- **Windows**: `%USERPROFILE%\AppData\LocalLow\<companyname>\<productname>`
- **macOS**: `~/Library/Application Support/<companyname>/<productname>`
- **Linux**: `~/.config/unity3d/<companyname>/<productname>`
- **Android**: `/storage/emulated/0/Android/data/<packagename>/files`
- **iOS**: `<app>/Documents`

### Profile Support
The platform cloud backup works seamlessly with MToolKit's profile system. Each profile's save files are stored in separate directories, and all are backed up by the platform services.

### No Additional Code Required
Unlike custom cloud sync solutions, platform cloud backup requires no additional code or server infrastructure. The platform services handle all the complexity of:
- Uploading files to cloud storage
- Downloading files on new installations
- Conflict resolution
- Network error handling
- User authentication

## Benefits Over Custom Cloud Sync

1. **No Server Costs**: No need to maintain cloud servers or pay for storage
2. **Better Reliability**: Platform services are highly reliable and maintained by major companies
3. **Automatic Setup**: Users don't need to configure anything - it works out of the box
4. **Better Security**: Platform services handle authentication and encryption
5. **Cross-Platform**: Works consistently across all supported platforms
6. **User Familiarity**: Users are already familiar with Steam Cloud, Google Drive, and iCloud

## Migration from Custom Cloud Sync

If you were previously using a custom cloud sync solution:

1. Remove custom cloud sync code
2. Enable platform cloud backup in `ES3SaveConfig`
3. Ensure save files are stored in `Application.persistentDataPath`
4. Test on target platforms to verify cloud backup is working

## Testing Cloud Backup

### Steam
1. Enable Steam Cloud for your game in Steam settings
2. Save game data
3. Uninstall and reinstall the game
4. Verify save data is restored

### Android
1. Enable Auto Backup in Android settings
2. Save game data
3. Uninstall and reinstall the app
4. Verify save data is restored from Google Drive

### iOS
1. Enable iCloud Backup in iOS settings
2. Save game data
3. Install on a different iOS device
4. Verify save data syncs via iCloud

## Troubleshooting

### Save Files Not Backing Up
- Verify `Application.persistentDataPath` is being used
- Check platform-specific cloud backup settings are enabled
- Ensure save files are being written to the correct directory

### Platform-Specific Issues
- **Steam**: Check Steam Cloud is enabled for your game in Steam settings
- **Android**: Verify Auto Backup is enabled in Android settings
- **iOS**: Ensure iCloud Backup is enabled and user is signed in to iCloud

## Future Considerations

This approach provides excellent cloud backup functionality with minimal complexity. If you need more advanced features like:
- Cross-platform save sharing (Steam ↔ Android ↔ iOS)
- Custom conflict resolution
- Real-time sync during gameplay

You may need to implement a custom solution, but for most games, platform-native cloud backup is the recommended approach.
