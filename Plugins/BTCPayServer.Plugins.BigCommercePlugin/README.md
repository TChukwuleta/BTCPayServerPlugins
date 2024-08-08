# Big Commerce plugin for BTCPayServer

This plugin allows you to integrate your [BigCommerce](https://bigcommerce.com/) application with BTCPay Server. 

With this you have the option of accepting Bitcoin payments for your store.

## Installation

1. Install the plugin from Plugins=>Add New=> BigCommerce
2. Restart BTCPay Server.
3. Navigate to and click the BigCommerce plugin as show on the navigation panel. There you would see all required URLs (Auth, load, uninstall).
4. Log in to your Big commerce application, and set up an application to be used for receiving bitcoin payment via BTCPay Server account.
5. Input the Auth Callback URL, Load Callback URL, and the Uninstall Callback URL with the URL in your BTCPay BigCommerce instance
6. For scopes and permissions:

![image](https://github.com/user-attachments/assets/a7770f65-fde9-408d-8643-daf7aa2345fb)

![image](https://github.com/user-attachments/assets/009b5150-4d31-4ec1-9ffa-3b614834165c)

7. Once you are done setting up your application on BigCommerce, copy the client Id and the client Secret, go to your BTCPay instance and update the credentials. Please ensure they are copied properly.

P.S: You cannot assign a BigCommerce application credential to multiple stores on BTCPay Server.

## Big Commerce Uninstall

Once you uninstall the application, the client Id and the client secret is also deleted from the store and can be reused by another store, or the same store with a new application
