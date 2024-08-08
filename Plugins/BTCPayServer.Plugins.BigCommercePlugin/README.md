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


Once you are done you can go ahead and install the application on your BigCommerce store. 

Login to your BigCommerce, Navigate to Apps => MyApps and install the Big Commerce plugin.

Once the installation is completed, you should see the plugin on the Apps section of your navigation panel.

One last step, we need to verify that the checkout script was updated successfully.  

Navigate to StoreFront => Script Manager.

Confirm you have a script with the name: btcpay-checkout. If you do, you are good to go.
If you dont, you would need to create a new script. 

In the create script form:

- Script name => btcpay-checkout
- Placement => Footer
- Location => Checkout
- Script Category => Essential
- Script type => URL
- Script URL => 
Copy the checkout script URL in your Bigcommerce plugin page on BTCPay Server. 
To the copied script URL, you would need to include a query parameter as show below:

https://domain.btcpayserver.com/plugins/B6XJFepkN61YkcHXT42vfwLK1bMdJEYi61nEyxVY4frW/bigcommerce/btcpay-bc.js?bcid={storehash}

Where store hash is replaced with your actual store hash. 
Don't know where to find your store hash? It is the set of characters in your BigCommerce store url beside store-

For instance, assuming your URL is https://store-test1234.mybigcommerce.com/

Your store hash is test1234

Using this example, your script url would be:

https://domain.btcpayserver.com/plugins/B6XJFepkN61YkcHXT42vfwLK1bMdJEYi61nEyxVY4frW/bigcommerce/btcpay-bc.js?bcid=test1234

Once completed, click the save button and you should be good to go. 

You can now receive payment for your store using BigCommerce

## Big Commerce Uninstall

Once you uninstall the application, the client Id and the client secret is also deleted from the store and can be reused by another store, or the same store with a new application
