# BTCPay Server Store Bridge Plugin

Accept Bitcoin and Lightning Network payments directly from the Lightspeed Retail point of sale. When a customer chooses to pay with Bitcoin, 
a BTCPay checkout appears inside the Lightspeed payment screen. Once the payment settles, Lightspeed automatically marks the sale as paid

## What you need?

- A running BTCPay Server instance (self-hosted or hosted by a third party)
- A Lightspeed retail account (ourstore.retail.lightspeed.app)

This plugin does not support Lightspeed Restaurant or Lightspeed eCom yet.


## Installation

1. On the left navigation of BTCPay Server UI click **Manage Plugins** search for **Light Speed** plugin
2. Install the plugin, and if BTCPay Server requests that you restart the instance, go ahead and restart.
3. Once the plugin is installed, you should see `Lightspeed HQ` in the left navigation. Click on it to open the settings page.
4. Enter your Lightspeed store URL (https://yourstore.retail.lightspeed.app) and then save. The plugin uses to validate payment requests coming from your Lightspeed store.
5. Copy the Gateway URL shown (https://yourbtcpay.com/plugins/STOREID/lightspeedhq/gateway)
6. Log into your Lightspeed retail account, and go to `Settings > Payment Methods > Add Payment Method`
7. Choose `Other psyment method`, name it whatever you choose (Pay with Bitcoin, Pay with BTCPay Server)
8. Paste the gateway url you copied from the plugin into the `Gateway URL` field and save.

That's it. The payment type will now appear on your Lightspeed sell screen.

### How a payment works

When a customer is ready to pay, the cashier clicks `Pay` on the sale in the Lightspeed and selects your Bitcoin payment type

Lightspeed opens a payment window of which BTCPay Server invoice checkout page is displayed. 

The customer can then go ahead and pay with any of the payment option available.

Once payment is confirmed, the window will close and the sale will be marked as paid in Lightspeed.

### Troubleshooting

##### The payment window opens but shows "Plugin not configured"
The store ID in the Gateway URL doesn't match a store with the plugin configured. Double-check that you copied the Gateway URL from the correct BTCPay store.

##### The payment window opens but shows "Invalid origin"
The Lightspeed Store URL saved in plugin settings doesn't match the actual domain your Lightspeed account is on. Go back to plugin settings and make sure the URL matches exactly, including https://.


## Contribute

This BTCPay Server plugin is built and maintained entirely by contributors around the internet. We welcome and appreciate new contributions.

Do you notice any errors or bug? are you having issues with using the plugin? would you like to request a feature? or are you generally looking to support the project and plugin please [create an issue](https://github.com/TChukwuleta/BTCPayServerPlugins/issues/new)

Feel free to join our support channel over at [https://chat.btcpayserver.org/](https://chat.btcpayserver.org/) or [https://t.me/btcpayserver](https://t.me/btcpayserver) if you need help or have any further questions.

