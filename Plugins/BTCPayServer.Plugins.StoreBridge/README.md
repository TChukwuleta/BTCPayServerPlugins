# BTCPay Server Store Bridge Plugin

A plugin for BTCPay Server that allows you to export and import complete store configurations between BTCPay Server instances.

## Features

- **Export stores** with complete configuration including:
  - Store settings (name, website, currency, policies)
  - Wallet configurations (xpubs only - never private keys)
  - Payment methods (on-chain and Lightning)
  - Webhooks
  - Store users and roles
  - Apps (Point of Sale, Crowdfund, etc.)

- **Import stores** with granular control over what gets imported
- **Security-first design** - private keys are never exported
- **Validation** to prevent importing invalid or dangerous data
- **Transaction-based imports** - all-or-nothing to maintain consistency

## Installation

### From BTCPay Server UI

1. Go to **Server Settings** > **Plugins**
2. Click **Upload Plugin**
3. Upload the compiled `.btcpay` plugin file
4. Restart BTCPay Server

### Building from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/btcpayserver-plugin-store-import-export
cd btcpayserver-plugin-store-import-export

# Build the plugin
dotnet build -c Release

# The plugin will be in bin/Release/net8.0/
```

### Installing Manually

1. Copy the built DLL to your BTCPay Server plugins directory:
   ```bash
   cp bin/Release/net8.0/BTCPayServer.Plugins.StoreImportExport.dll \
      /path/to/btcpayserver/Plugins/
   ```

2. Restart BTCPay Server

## Usage

### Exporting a Store

1. Navigate to your store settings
2. Click on **Export Store** in the sidebar
3. Review what will be exported
4. Click **Download Store Export**
5. Save the JSON file securely

### Importing a Store

1. Go to **Store Settings** or create a new store context
2. Click on **Import Store** in the sidebar
3. Upload the exported JSON file
4. Configure import options:
   - Choose which components to import
   - Optionally rename the store
   - Decide whether to import users
5. Click **Import Store**
6. Review the import results

### Import Options

- **Import Wallets**: Import wallet derivation schemes (xpubs)
- **Import Payment Methods**: Import payment method configurations
- **Import Webhooks**: Import webhook URLs, events, and secrets
- **Import Users**: Add existing users to the store (users must exist on target server)
- **Import Apps**: Import Point of Sale, Crowdfund, and other apps

## Security Considerations

### What Gets Exported

✅ **Safe to export:**
- Store settings and configuration
- Extended public keys (xpubs)
- Payment method configurations
- Webhook URLs and secrets (encrypted in transit)
- User email addresses and roles
- App configurations

❌ **Never exported:**
- Private keys or seed phrases
- Hot wallet private keys
- Lightning node macaroons or credentials
- User passwords
- API key secrets (regenerate after import)

### Best Practices

1. **Store export files securely** - they contain sensitive configuration data
2. **Use HTTPS** when transferring export files
3. **Verify data** before importing on production servers
4. **Regenerate secrets** after import (API keys, webhook secrets if needed)
5. **Review imported configuration** before going live
6. **Backup before importing** on production environments

## Post-Import Tasks

After importing a store, you'll need to:

1. **Configure Lightning nodes** - Connection strings are not exported
2. **Verify wallet access** - Ensure you have the corresponding private keys
3. **Test payment methods** - Confirm all payment methods work correctly
4. **Review webhooks** - Verify webhook endpoints are accessible
5. **Update API integrations** - Regenerate API keys if needed
6. **Test apps** - Verify Point of Sale and other apps function correctly

## Troubleshooting

### Import Fails with "Private keys detected"

The export file should never contain private keys. If you see this error, the export file may be corrupted or tampered with.

### Users Not Imported

Users must already exist on the target BTCPay Server. Create user accounts first, then re-import with the "Import Users" option enabled.

### Wallets Not Working After Import

The import only includes xpubs. You need to ensure you have access to the corresponding private keys/seeds and configure any hot wallets separately.

### Lightning Not Working

Lightning node connection strings are not exported for security. Reconfigure your Lightning node connections after import.

## Development

### Project Structure

```
BTCPayServer.Plugins.StoreImportExport/
├── Controllers/
│   └── StoreImportExportController.cs    # MVC controller
├── Models/
│   └── StoreExportModels.cs              # DTOs for export/import
├── Views/
│   ├── StoreImportExport/
│   │   ├── Export.cshtml                 # Export UI
│   │   └── Import.cshtml                 # Import UI
│   └── Shared/
│       └── StoreNav.cshtml               # Navigation menu
├── StoreImportExportPlugin.cs            # Plugin entry point
├── StoreImportExportService.cs           # Business logic
└── BTCPayServer.Plugins.StoreImportExport.csproj
```

### Extending the Plugin

To add support for additional data:

1. Add properties to `StoreExportData` in `StoreExportModels.cs`
2. Implement export logic in `StoreImportExportService.cs`
3. Implement import logic with validation
4. Add UI options in the Import view
5. Update documentation

### Testing

```bash
# Run unit tests
dotnet test

# Test in development BTCPay instance
dotnet run --project /path/to/btcpayserver
```

## Roadmap

- [ ] Support for encrypted exports with password protection
- [ ] Incremental imports (update existing stores)
- [ ] Export templates (partial configurations)
- [ ] API endpoints for programmatic import/export
- [ ] Support for batch operations (multiple stores)
- [ ] Import preview/dry-run mode
- [ ] Migration from other payment processors

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes with tests
4. Submit a pull request

## License

MIT License - see LICENSE file for details

## Support

- **Documentation**: https://docs.btcpayserver.org
- **Community**: https://chat.btcpayserver.org
- **Issues**: https://github.com/yourusername/btcpayserver-plugin-store-import-export/issues

## Disclaimer

This plugin is provided as-is. Always test thoroughly in a development environment before using in production. The authors are not responsible for any loss of funds or data.
