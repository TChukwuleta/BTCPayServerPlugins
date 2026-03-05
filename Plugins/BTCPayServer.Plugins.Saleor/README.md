# BTCPay Server Saleor 

Accept bitcoin payments from Saleor based storefronts. 

## What you need?

- A running BTCPay Server instance (self-hosted or hosted by a third party)
- A running Saleor instance 

## Installation

1. On the left navigation of BTCPay Server UI click **Manage Plugins** search for **Saleor** plugin
2. Install the plugin, and if BTCPay Server requests that you restart the instance, go ahead and restart.
3. Once the plugin is installed, you should see `Saleor` in the left navigation. Click on it to open the settings page.
4. Copy the manifest URL from the Saleor plugin page, and head over to Saleor store dashboard.
5. On the left navigation panel click on `Extensions` > `Installed` > `Add Extension` > `Install from manifest` and paste the manifest URL you copied in the manifest url input. Click `Install` to install the plugin extension in Saleor.
6. If you run a storefront, please ensure that you include `redirectUrl` in the data object of your `transaction initialie` webhook call, and it should be the URL of the page you want your customers to be redirected to after they complete the payment.

That's it. Now you can now receive Bitcoin and Lightning Network payments in your Saleor Instance and get your order and payment completed.


To confirm everything is working end-to-end:

Add an item to your storefront cart and proceed to checkout. Select `BTCPay Server` as the payment method, and click pay. You'd be redirected to BTCPay Server's checkout page to complete the payment.

After payment, BTCPay should redirect you back to your storefront. You can confirm the order appears as paid in your Saleor Dashboard


## Troubleshooting

#### Not redirected back to storefront after payment
The redirectUrl was not passed in the data field of transactionInitialize. Check your frontend mutation call. Regardless the payment should be confirmed and completed on Saleor orders dashboard as long as payment was made

#### Order stays pending after payment is confirmed
BTCPay's webhook to Saleor may have failed. Check BTCPay Server's Store > Webhooks delivery logs for errors.

#### 401 Unauthorized errors on webhooks
The Saleor API URL or App Token may be mismatched. Re-check the values in Store > Saleor in BTCPay Server.

#### App not appearing as a payment option at checkout
Verify the app has HANDLE_PAYMENTS permission in Saleor and is assigned to the correct channel.


## Contribute

This BTCPay Server plugin is built and maintained entirely by contributors around the internet. We welcome and appreciate new contributions.

Do you notice any errors or bug? are you having issues with using the plugin? would you like to request a feature? or are you generally looking to support the project and plugin please [create an issue](https://github.com/TChukwuleta/BTCPayServerPlugins/issues/new)

Feel free to join our support channel over at [https://chat.btcpayserver.org/](https://chat.btcpayserver.org/) or [https://t.me/btcpayserver](https://t.me/btcpayserver) if you need help or have any further questions.

