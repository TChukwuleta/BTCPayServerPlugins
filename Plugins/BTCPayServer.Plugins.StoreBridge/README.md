# BTCPay Server Store Bridge Plugin

Export and import store configurations in BTCPay Server.

## What can you export/import?

- Branding
- Email Settings
- Rate Settings
- Checkout Configuration
- Webhooks
- Roles and Permissions
- Custom Forms


## Installation

1. On the left navigation of BTCPay Server UI click **Manage Plugins** search for **Store Bridge** plugin
2. Install the plugin, and if BTCPay Server requests that you restart the instance, go ahead and restart.

### Usage

Now that you have installed the plugin, you can go ahead and export store settings and configuration and import into either a different store
in the same instance or across other instance. 

Go to `Export Plugin Config` of the Store Bridge plugin. There you would see all the store configuration that are open to be exported. 
Toggle off the settings/configuration you don't want to export. Once done, click the `Export Store` button and it would download
.storebridge file on your local computer. 

Log in to the store you want to import the configuration to. Go to the Store bridge plugin > `Import Store Config`, and upload the 
.storebridge file that was downloaded in the export. 

There you would see all the available store configuration that you can import based on what was selected during export. 

You can go ahead and import all settings, or select specific configuration you want to import to the store. Once done, click on 
`Confirm Import` button and voila.. Store configuration have now been imported to the store.


### What Each Setting Does

1. Branding Settings: Logo image, custom CSS, brand color scheme, backend appearance settings
2. Email Settings: SMTP configuration
3. Rate Settings: Primary rate, fallback rate, speed policy
4. Checkout Settings: Default language, UI customization option, monitoring interval, invoice expiration timer e.t.c.
5. Webhook: All existing webhook configuration
6. Forms: All created custom forms. 
7. Roles and Permission: All roles together with their corresponding permissions


## Roadmap

- [ ] Support for encrypted exports with password protection
- [ ] API endpoints for programmatic import/export
- [ ] Support for batch operations (multiple stores)


## Contribute

This BTCPay Server plugin is built and maintained entirely by contributors around the internet. We welcome and appreciate new contributions.

Do you notice any errors or bug? are you having issues with using the plugin? would you like to request a feature? or are you generally looking to support the project and plugin please [create an issue](https://github.com/TChukwuleta/BTCPayServerPlugins/issues/new)

Feel free to join our support channel over at [https://chat.btcpayserver.org/](https://chat.btcpayserver.org/) or [https://t.me/btcpayserver](https://t.me/btcpayserver) if you need help or have any further questions.

