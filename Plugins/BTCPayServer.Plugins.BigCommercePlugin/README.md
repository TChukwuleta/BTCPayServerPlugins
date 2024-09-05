# Accept Bitcoin in BigCommerce with BTCPay Server

Introducing BTCPay Server for BigCommerce – an open-source payment gateway that enables you accept Bitcoin payments directly on your website or store from customers with no fees.

Our integration with [BigCommerce](https://bigcommerce.com/) allows you to connect your self-hosted BTCPay Server with your BigCommerce store, enabling you to accept Bitcoin payments seamlessly.

## Why Choose BTCPay for Your BigCommerce Store?

- **No Fees, No Hidden Costs**: BTCPay Server empowers your business with a truly fee-free payment solution. Enjoy every cent of your transactions without any deductions
- **Direct payment**: Say goodbye to intermediaries and lengthy KYC processes. Receive payments directly to your wallet with complete privacy
- **Automated Payment Processing**: BTCPay Server takes care of payments and invoice management automatically.
- **Great Checkout Experience**: Enhance customer experience with Bitcoin QR code displays during checkout.
- **Self-hosted infrastructure**: With BTCPay Server, you own your payment instances. You also maintain complete control over your payment infrastructure.
- **Easy data exports**: Easily export payment data with CSV files, making financial management a breeze.
- **Multilingual ready**: Serve a global audience right out of your corner.
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

P.S: It is important to note that you cannot assign a BigCommerce application credential to multiple stores on BTCPay Server.


### Install the app to your store and connect

Once you have completed the above steps, it is time to install your BigCommerce application in your store and start receiving payment using Bitcoin.

1. Login to your [BigCommerce](https://docs.btcpayserver.org/WalletSetup/), Navigate to Apps => MyApps
2. Select "My Draft Apps", there you would see a list of all your BigCommerce application that you've set up including this newly created application.
3. Hover on your newly created application and click on "Learn more"
4. It would take you to a page containing your application details including permissions granted to the application, with a button to install.

![image](https://github.com/user-attachments/assets/a6b2ea8b-5d2b-44ee-a359-d471cc52a834)

5. Click on the install button, acknowledge that PCI-DSS compliance, and confirm the installation.

![image](https://github.com/user-attachments/assets/aa2dd84a-d54a-4f10-83e7-f7b9bc9c4e57)

6. Once the installation is successful, you should see the application listed on the Apps section of your navigation panel.

You manually need to create an offline payment method containing "Bitcoin" (e.g. Bitcoin / Lightning Network) in the BigCommerce store under Settings => Setup => Payments => Additional providers

### Verify script installation

One last step, we need to verify that the checkout script was updated successfully on your store.

1. Navigate to StoreFront => Script Manager.
2. Confirm you have a script with the name: btcpay-checkout, with date-installed corresponding to the date at which the app was installed. If you do, you are good to go.
3. If you dont, kindly verify the permissions granted and retry the installation process.

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
