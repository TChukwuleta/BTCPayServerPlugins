# Big Commerce plugin for BTCPayServer

This plugin allows you to integrate your [BigCommerce](https://bigcommerce.com/) application with BTCPay Server. 

With this you have the option of accepting Bitcoin payments for your store.

## Installation

1. Install the plugin from Plugins=>Add New=> BigCommerce
2. Restart BTCPay Server
3. Log in to your Big commerce application, and set up an application to be used for receiving bitcoin payment via BTCPay Server account
4. Copy the client Id and the client Secret of the created application
5. Go back to your BTCPay Server, choose the store to integrate with and click on Big Commerce in the navigation. 
6. Enter the Client Id and secret and click create. (P.S: For multiple store integrations, kindly use unique client Id and secret for each store)
7.Once completed, the plugin would then display your auth callback url, load url, and uninstall url..
8. Log back to your big commerce application and update your app with the three url.


## Big Commerce Uninstall

Once you uninstall the application, the client Id and the client secret is also deleted from the store and can be reused by another store, or the same store with a new application
