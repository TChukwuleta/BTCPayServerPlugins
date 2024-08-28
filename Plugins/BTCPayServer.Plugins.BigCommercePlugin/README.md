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

### Install the plugin on BTCPay Server

1. In your BTCPay instance, navigate to Plugins=> Add New => BigCommerce
2. Restart BTCPay Server.
3. If successful, you'd see Bigcommerce included in the plugin section.

![image](https://github.com/user-attachments/assets/fd08535d-8a6a-4d94-a55c-317b297858c1)

Click on it, it would display  BigCommerce configuration page for BTCPay server where you can configure your credentials and also view your callback Urls.

### Setup the BigCommerce app

1. Now, you need to create a BigCommerce application. go to [devtools.bigcommerce.com](https://devtools.bigcommerce.com) and click on Create an app button.
2. After inputting the name of the application, you'd need to fill more details about the application including the callback urls, permission and scopes, etc.
   For the callback urls (auth, load, uninstall), you can copy it from your Bigcommerce plugin page on BTCPay Server, and prefill the inputs on your BigCommerce app.

![image](https://github.com/user-attachments/assets/c359d350-54cd-465b-8b75-b4b55c23e5a0)

3. There are also required permissions that you will need to grant the BigCommerce application for the integration to be successful. So from the OAuth scopes list, ensure you select the
following permissions and their access levels.
   - Orders => Modify
   - Order Transactions => Modify
   - Content => Modify
   - Checkout Content => Modify
   - Carts => Modify
   - Checkouts => Modify
   - Information & Settings => Read-Only

![image](https://github.com/user-attachments/assets/a49d2d5e-8d28-4f8e-97d4-bfac64bd0b24)

![image](https://github.com/user-attachments/assets/9bbfc66d-e6c7-4ba9-8f47-16ed6eab29dd)

4. Go ahead and fill out other information about the application, once completed you can save.
5. Once your application is included in the app list, click on the "View Client Id" icon of the just created application to get the client Id and secret of the BigCommerce application.
6. Go back to your BigCommerce plugin on BTCPay server, and update the configuration details with the client Id and secret as copied from the BigCommerce application. Please ensure they are copied properly.
     


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

P.S. You need to ensure that the required permissions listed above are granted to the application before creating a new payment script.

## How to receive payment:

On the checkout page, after all necessary information has been inputted, the payment screen would be similar to this:

![image](https://github.com/user-attachments/assets/afde799b-5c27-470c-b175-fb27233e6ff8)

From the payment providers listed, select "Bitcoin/Lightning Network". 

![image](https://github.com/user-attachments/assets/da589c8f-f3de-4c63-9776-36aae9be574d)

Notice that the button now changes to "Pay with Bitcoin".

To complete the payment, the customer clicks on the the "Pay with Bitcoin" button. The QR code would pop on the screen with an option to pay directly through the customer's wallet.

![image](https://github.com/user-attachments/assets/0f3073c3-e0e5-464e-b879-0bfe37384eaf)

Once the payment is complete and confirmed, an invoice is then successfully created on the merchant BTCPay instance as well as the payment. 


## Big Commerce Uninstall

Once you uninstall the application, the client Id and the client secret is also deleted from the store and can be reused by another store, or the same store with a new application.
So if you uninstalled, and you want to still use the same store, go back to your BTCPay instance and save the clientId and secret.
