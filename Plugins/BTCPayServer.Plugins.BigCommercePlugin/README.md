# Accept Bitcoin in BigCommerce with BTCPay Server

Introducing BTCPay Server for BigCommerce – an open-source payment gateway that empowers you to accept Bitcoin payments directly on your website or store from customers with no fees.

Our integration with [BigCommerce](https://bigcommerce.com/) allows you to connect your self-hosted BTCPay Server with your BigCommerce store, enabling you to accept Bitcoin payments quickly and securely.

## Why Choose BTCPay for Your BigCommerce Store?

- **No Fees, No Hidden Costs**: BTCPay Server empowers your business with a truly fee-free payment solution. Enjoy every cent of your transactions without any deductions
- **Direct payment**: Say goodbye to intermediaries and lengthy KYC processes. Receive payments directly to your wallet with complete privacy
- **Automated Payment Processing**: BTCPay Server takes care of payments, invoice management automatically.
- **Great Checkout Experience**: Enhance customer experience with Bitcoin QR code displays during checkout.
- **Self-hosted infrastructure**: With BTCPay Server, you own your payment instances. You also maintain complete control over your payment infrastructure.
- **Easy CSV exports**: Easily export payment data with CSV files, making financial management a breeze.
- **Multilingual ready**: Serve a global audience right out of the box.
- **Community-driven support**: Get responsive assistance from our dedicated community ([Mattermost](http://chat.btcpayserver.org/) or [Telegram](https://t.me/btcpayserver)).

## Prerequisites:

Before diving into the setup process, ensure you have the following:

- [A BigCommerce Account](https://login.bigcommerce.com/)
- BTCPay Server - [self-hosted](https://docs.btcpayserver.org/Deployment/) or run by a [third-party host](https://docs.btcpayserver.org/Deployment/ThirdPartyHosting/) v1.4.8 or later.
- [Created BTCPay Server store](https://docs.btcpayserver.org/CreateStore/) with [wallet set up](https://docs.btcpayserver.org/WalletSetup/)
  
## Setting up BTCPay Server with BigCommerce

1. Install the plugin from Plugins=>Add New=> BigCommerce
2. Restart BTCPay Server.
3. Navigate to and click the BigCommerce plugin as show on the navigation panel. There you would see all required URLs (Auth, load, uninstall).
4. Log in to your Big commerce application, and set up an application to be used for receiving bitcoin payment via BTCPay Server account.
5. Input the Auth Callback URL, Load Callback URL, and the Uninstall Callback URL with the URL in your BTCPay BigCommerce instance
6. To install the Plugin successfully, and to be able to receive payment via Bitcoin, you'd need to register some permissions on BigCommerce. Below are the required OAuth Scopes needed for the BigCommerce app
   - Orders => Modify
   - Order Transactions => Modify
   - Content => Modify
   - Checkout Content => Modify
   - Carts => Modify
   - Checkouts => Modify
   - Information & Settings => Read-Only

![image](https://github.com/user-attachments/assets/a49d2d5e-8d28-4f8e-97d4-bfac64bd0b24)

![image](https://github.com/user-attachments/assets/9bbfc66d-e6c7-4ba9-8f47-16ed6eab29dd)
     

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
