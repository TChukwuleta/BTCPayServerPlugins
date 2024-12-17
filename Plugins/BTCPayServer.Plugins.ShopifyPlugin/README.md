# Accept Bitcoin in Shopify with BTCPay Server

Introducing BTCPay Server for Shopify – open-source payment gateway that enables you accept bitcoin payments directly on your website or stores from customers with no fee.

Our integration with Shopify allows you connect your self-hosted BTCPay Server with your [Shopify store](https://www.shopify.com/), enabling you accept Bitcoin payments swiftly and securely.


## What BTCPay offers:

- **Zero fees**: Enjoy a payment gateway with no fees. Yes, You saw that right. Zero fees!
- **Direct payment, No middlemen or KYC**: Say goodbye to intermediaries and tedious paperwork, and get your money directly in your wallet
- **Fully automated system**: BTCPay takes care of payments, invoice management and refunds automatically.
- **Display Bitcoin QR code at checkout**: Enhance customer experience with an easy and secure payment option.
- **Self-hosted infrastructure**: Maintain full control over your payment gateway.
- **Lightning Network integrated**: Instant, fast and low cost payments and payouts
- **Easy CSV exports**
- **Versatile plugin system**: Extend functionality according to your needs
- **Point-of-sale integration** – Accept payments in your physical shops
- **Multilingual ready**: Serve a global audience right out of the box.
- **Community-driven support**: Get responsive assistance from our dedicated community ([Mattermost](http://chat.btcpayserver.org/) or [Telegram](https://t.me/btcpayserver)).


## Prerequisites:

Before diving into the setup process, ensure you have the following:

- Shopify account
- BTCPay Server - [self-hosted](Deployment.md) or run by a [third-party host](/Deployment/ThirdPartyHosting.md) v2.0.0 or later.
- [Created BTCPay Server store](CreateStore.md) with [wallet set up](WalletSetup.md)

[![BTCPay Server - Shopify Video](https://img.youtube.com/vi/jJjAyvgWVfk/mqdefault.jpg)](https://www.youtube.com/watch?v=jJjAyvgWVfk)

:::warning
Shopify might give false flags on deprecated API calls. The most likely answer we could find, due to a change by Shopify to specific fields of the `Order` resource and is assumed to get used in BTCPay Server. Find more details [here](https://github.com/btcpayserver/btcpayserver/issues/4510)
:::

## Setting up BTCPay Server with Shopify

### Create an app and get API credentials from Shopify
1. In Shopify, click on `Apps >` in the left sidebar   
![BTCPay Server shopify step 1](./img/Shopify/step_1.png)
2. On the modal popped up, click on `App and sales channel settings`   
![BTCPay Server shopify step 2](./img/Shopify/step_2.png)
3. From the page displayed, click on `Develop apps` button   
![BTCPay Server shopify step 3](./img/Shopify/step_3.png)
4. If prompted, click on `Allow custom app development`   
![BTCPay Server shopify step 4](./img/Shopify/step_4.png) 
5. `Create an app` and name it, e.g. BTCPay Server, click on `Create App`      
![BTCPay Server shopify step 5](./img/Shopify/step_5.png)
![BTCPay Server shopify step 6](./img/Shopify/step_6.png)
6. On the app page, in `Overview` tab, click on the `Configure Admin API scopes`   
![BTCPay Server shopify step 7](./img/Shopify/step_7.png)
7. In the filter admin access scopes type in `Orders`
8. In `Orders` enable `read_orders` and `write_orders` and then click `Save`     
![BTCPay Server shopify step 8](./img/Shopify/step_8.png)   
9. Click on the "API credentials" tab, and then click on the `Install App` in the top right corner and when pop-up window appears click `Install`   
![BTCPay Server shopify step 9-1](./img/Shopify/step_9-1.png)   
![BTCPay Server shopify step 9-2](./img/Shopify/step_9-2.png)   
10. Reveal `Admin API access token` and `copy` it (and note it down somewhere safe)   
11. Also copy the `API key` and `API Secret` and note it down somewhere safe     
![BTCPay Server shopify step 10 and 11](./img/Shopify/step_10_and_11.png)   
12. Shopify app setup is now complete

### Set up a custom payment method in Shopify
1. Back to Shopify, on the home page, click on `Settings` >> `Payments` in the left sidebar, scroll down to "Manual payment methods", click on `(+) Manual payment method` and select `Create custom payment method` on the dropdown.
   ![Create payment method step 1](./img/Shopify/pm_step_1.png)
2. In `Custom payment method name` fill in `Bitcoin with BTCPay Server` (also see TIP box below), optionally you can fill in other fields, but it's not required.
   However you would need to inform your customers that payment with Bitcoin comes on the next screen after checkout on the "Thank you" page. Ideally you would inform your customers in the `Additional details` field.
   The payment option can have a delay between 2 - 10 seconds on the "Thank you" page, before it is displayed so this also needs to be communicated with the customers. Suggested text: `Please note that the Bitcoin payment option will be displayed on the "Thank you" page after a few seconds. If it does not show up after 5-10 seconds please contact our support.`
3. Hit `Activate` and you've set up Shopify and BTCPay Server payment method successfully.
   ![Create payment method step 2 and 3](./img/Shopify/pm_step_2_and_3.png)

:::tip
"Custom Payment method name" **must** contain at least one of the following words (case-insensitive): `bitcoin`, `btcpayserver`, `btcpay server` or `btc` to work.
:::

## Install BTCPay Server Shopify plugin
1. In your BTCPay Server, go to your plugins, find and install Shopify plugin. Once done, on the left sidebar click on `Shopify`
2. In the first field, `Shopify Store URL` enter the subdomain of your Shopify store e.g. SOME_ID.myshopify.com then enter SOME_ID
3. In the second field, `API key` paste the `API key` from Shopify - see steps above.
4. Do the same for the third field, paste the `API Secret` from Shopify and paste in the `API Secret` field.
5. In the last field, `Admin API access token` paste the `Admin API access token` 
6. You can decide to edit the payment method description text. This basically defines the text that the user sees when the invoice loads on shopify.
7. Click `Save` on BTCPay Shopify settings page   
![BTCPay Server step 1-7](./img/Shopify/btcpay_step_1-7.png)
8. BTCPay then validates the credentials, and once validated, creates an create order webhook, and finally saves the credentials.   
![BTCPay Server step 1-7](./img/Shopify/btcpay_step_8.png)

   

## Install BTCPay-Shopify application on Shopify

The second piece of this installation guide is setting up the BTCPay-Shopify app. You can choose to self-host your own BTCPay-Shopify app 
or use the app we submitted to the Shopify App Store.

### Using BTCPay-Shopify from Shopify App Store

:::warning
As of now, the BTCPay-Shopify app is not yet available on the Shopify App Store. We are working on getting it published.
:::

1. Now we need to install the BTCPay-Shopify app. Visit the [Shopify app store](https://apps.shopify.com/) search for "BTCPay" and install the BTCPay-Shopify app.
2. Once installed, you can go to your dashboard, click on `Apps >` in the left sidebar, then you would see the list of installed apps, including the just installed BTCPay-Shopify app. 
3. Select the BTCPay-Shopify app, and in settings, input your `BTCPay Server URL` (e.g https://btcpay.example.com) and the `storeId` to which your Shopify plugin is connected on your BTCPay Server instance.

![BTCPay Server shopify step 13](https://github.com/user-attachments/assets/40469ad5-6545-4231-85ff-c9e35e729f76)

![BTCPay Server shopify step 14](https://github.com/user-attachments/assets/f498488c-e0e6-4ac6-8cab-fdb3db5a2746)

4. In your shopify dashboard, click on `Settings`, which is located on the bottom of the left nav panel, select `Checkout` and then `Customize`
5. In the editor change the selected page to the Thank you page

![BTCPay Server shopify step 15](https://github.com/user-attachments/assets/62c850db-dfaf-46c2-b7a3-4d911e96bd15)

![BTCPay Server shopify step 16](https://github.com/user-attachments/assets/a8651278-0272-47bd-b685-a55eb41de6c9)

![BTCPay Server shopify step 17](https://github.com/user-attachments/assets/cebb68c7-9593-4c52-ad49-89d2858f155f)

![BTCPay Server shopify step 18](https://github.com/user-attachments/assets/cde0f703-dd72-45c0-9746-e9462a3b77ab)

6. Click on the `Apps` icon on the left panel > `Add app block` on the bottom of the section tab
7. Click on `Sections` icon on the left panel > `Order details` > `Add app block` on the bottom of the section tab
8. Select the BTCPay Server extensions.
9. Click `Save` button on the top right corner of the page to save the changes


![BTCPay Server shopify step 19](./img/Shopify/step_19.png)

![BTCPay Server shopify step 21](https://github.com/user-attachments/assets/4756be6d-513d-41c0-9e28-4c5b1086cd84)

![BTCPay Server shopify step 22](https://github.com/user-attachments/assets/bdc64c5e-af81-417e-b247-85c610687fb7)

![BTCPay Server shopify step 23](https://github.com/user-attachments/assets/19cf8823-23a5-4c40-8a1d-8db56ae2b54b)

![BTCPay Server shopify step 20](./img/Shopify/step_20.png)

### Self-hosting the BTCPay-shopify app

If you are a developer or would love to have total control, you would need to create and deploy your own application.
#### Requirements:
1. A Linux VPS instance to deploy your shopify app to
2. The VPS should have [Docker Engine installed](https://docs.docker.com/engine/install/)
3. A domain/subdomain with an DNS A-record to the IP of your VPS instance, in our example below we use the placeholder "YOUR_HOSTED_APP_URL.COM", e.g. btcpaypp.example.com
4. Shopify plugin installed in your BTCPay Server instance
5. A [shopify partner account](https://www.shopify.com/partners)

#### Installation instructions: 

##### Create a Shopify app on partner account
1. On Shopify Partner [dashboard](https://partners.shopify.com), click on `Apps` > `All Apps` > `Create App` > `Create app manually`. Enter the name you want to call the app (e.g. BTCPay Server APPNAME) and click `Create`.
2. Once created displays your "Client ID" and "Client secret", which we need in a minute. 
3. On the left sidebar click on `Configuration`
4. In the `App URL` field, enter the URL of your hosted app, e.g. `https://YOUR_HOSTED_APP_URL.COM`
5. In the `Allowed redirection URL(s)` field, enter:
``` 
https://YOUR_HOSTED_APP_URL.COM/auth/callback
https://YOUR_HOSTED_APP_URL.COM/auth/shopify/callback
https://YOUR_HOSTED_APP_URL.COM/api/auth/callback
```
6. In the fields in the "Compliance webhooks" section, enter the following:
`Customer data request endpoint` => https://YOUR_HOSTED_APP_URL.COM/webhooks/customers/data_request
`Customer data erasure endpoint` => https://YOUR_HOSTED_APP_URL.COM/webhooks/customers/redact
`Shop data erasure endpoint` => https://YOUR_HOSTED_APP_URL.COM/webhooks/shop/redact
[Shopify-App: configuration.png](./img/Shopify/partner-app_configuration.png)
7. Click on `Save` to save the changes
8. On the left sidebar click on `API Access`
9. Scroll down to "Allow network access in checkout and account UI extensions" and click on `Request access`
![Shopify-App: Allow network access](./img/Shopify/partner-app_allow-network-access.png)
:::tip
When you get an error "Could not grant checkout ui extension scope 'read_checkout_external_data' when you try to enable network access, then go to your partner profile and fill out the first and last name and it will work.
:::

##### Deploy the BTCPay-Shopify app 

1. Next on your VPS switch to root user and clone or download [this repository](https://github.com/btcpayserver/btcpayshopifyapp) and go into that directory
   ```bash
   git clone https://github.com/btcpayserver/btcpayshopifyapp.git
   cd btcpayshopifyapp
   ``` 

2. Copy `.env.example` to `.env` file, it contains the following environment variables:
- DATABASE_URL => Your database connection string, keep it as is if you are using the default sqlite database
- SHOPIFY_API_KEY => Represents the "Client ID" associated with the shopify app created (step 2 above)
- SHOPIFY_API_SECRET => Represents the "Client Secret" associated with the shopify app (step 2 above)
- SHOPIFY_APP_URL => Internal URL to reach your app, e.g. `http://localhost:3000`
- DOMAIN => Your app domain, e.g. YOUR_HOSTED_APP_URL.COM
- LETSENCRYPT_EMAIL => Your email address for Let's Encrypt

```dotenv
DATABASE_URL=file:/app/data/database.sqlite
SHOPIFY_APP_URL=http://localhost:3000
SHOPIFY_API_KEY=your_client_id
SHOPIFY_API_SECRET=your_client_secret
DOMAIN=YOUR_HOSTED_APP_URL.COM
LETSENCRYPT_EMAIL=johndoe@example.com
```
Replace the values of `SHOPIFY_API_KEY`, `SHOPIFY_API_SECRET`, DOMAIN and `LETSENCRYPT_EMAIL` with your values. Don't change the value of `DATABASE_URL` and `SHOPIFY_APP_URL`.

3. Next, rename `shopify.app.toml.example` to `shopify.app.toml` and change the following values:
	Change the value of `client_id` to your apps Client ID. (Same value as SHOPIFY_API_KEY) 
	Change `name` to the name of your app (created in step 1. above) e.g. `name = "BTCPay Server APPNAME"`
    Change `handle` to the handle of your app (created in step 1. above, you can see it in  e.g. `handle = "btcpay-server-appname"`
    Change `application_url` to your deployed URL. E.g. `application_url = "https://YOUR_HOSTED_APP_URL.COM"`
    Change the value of `dev_store_url` to your shopify store url. E.g. `dev_store_url = "https://yourdevstore.myshopify.com"`
    In the `redirect_urls` array, replace YOUR_HOSTED_APP_URL.COM with your deployed URL and keep the paths. E.g. `https://YOUR_HOSTED_APP_URL.COM/auth/callback`

4. Next we need to edit the file located in `extensions/btcpaycheckout/src/Checkout.jsx` and replace the value of the constant `shopifyApplicaitonUrl` which is `https://YOUR_HOSTED_APP_URL.COM` with your deployed URL.

5. Now you can run `docker compose up -d` and it will spin up a nodeJS, Nginx and Let's encrypt container making sure the app is reachable over SSL. It will also install all the dependencies needed for the app to run.
6. Once done you need to go into the container and deploy a release. `docker exec -it shopify-app sh`
7. Now run `npm run deploy`. It will ask you to hit a key and it will show an authentication URL.
![App deploy: deploy and auth page](./img/Shopify/app-deploy_loginto-container-auth-start.png)
8. Copy the URL into the browser and login to your partner account.
![App deploy: login to partner account](./img/Shopify/app-deploy_partner_login_email.png)
9. Confirm and login.
![App deploy: Confirm CLI login](./img/Shopify/app-deploy_login-confirmation.png)
10. You should see a success message in the browser and the terminal should continue.
![App deploy: Login successful](./img/Shopify/app-deploy_login_success.png)
11. On terminal it will ask you if you want to release a new version, hit 'enter' to confirm. It will deploy and show you a success message.
![App deploy: Deployment successful](./img/Shopify/app-deploy_deployment_done.png)

12. Once deployed let's double-check a few things. 
    Go back to your shopify partner app dashboard and on left sidebar click on `Versions`, you should see a new version with the same timestamp as to when you deployed. 
    In your browser visit https://YOUR_HOSTED_APP_URL.COM and you should see a screen similar to this:
![App deployment: Check app deployed in browser](./img/Shopify/app-deploy_deployment_url_browser.png)

Congrats! You have successfully deployed the BTCPay-Shopify app, only a few steps left.

13. Now it is time to deploy your application to the Shopify store that you are linking to BTCPay server. On your partner account app overview, click on `Choose distribution` and select `Custom distribution`. Confirm the selection.
:::tip
Please note that selecting custom distribution would mean that you can only use the application on only one Shopify store. This is irreversible. You can deploy multiple apps though if you have more than one store.
:::
![App deploy: select custom distribution](./img/Shopify/app-deploy_custom-distribution-1.png)
![App deploy: confirm custom distribution](./img/Shopify/app-deploy_distribution-confirm.png)

14. On the next screen enter the Shopify store URL that you want to link the application to. This is typically the internal store url you see on configuring the store, e.g. something-random.myshopify.com.
![App deploy: enter your store url](./img/Shopify/app-deploy_distribution-generate-link.png)
15. Click on `Generate link` and you will see a link generated.
![App deploy: link generated](./img/Shopify/app-deploy_distribution-generated-link-copy.png)
15. Open the link generated on a new tab. Select the store to install the app on (ensure it matches with the store you just set in the URL field).
![app-deploy_distribution-choose-store.png](./img/Shopify/app-deploy_distribution-choose-store.png)
16. You will see your app listed and you can now install it by clicking on `Install`.
![app-deploy_distribution-install-to-store-confirm.png](./img/Shopify/app-deploy_distribution-install-to-store-confirm.png)
17. Once installed, you will see the app settings page. 
![app-deploy_distribution-install-complete-config-app.png](img/Shopify/app-deploy_distribution-install-complete-config-app.png)

Go back to [Using BTCPay-Shopify from Shopify App Store](#using-btcpay-shopify-from-shopify-app-store) section above and continue from step 3.


## Demo Checkout flow after everything is set up:

![BTCPay Server shopify step 30](./img/Shopify/complete_payment.png)

![BTCPay Server shopify step 31](./img/Shopify/payment_option.png)

![BTCPay Server shopify step 32](./img/Shopify/complete_payment.png)

![BTCPay Server shopify step 33](./img/Shopify/pay_with_btcpay_modal.png)

![BTCPay Server shopify step 34](./img/Shopify/pay_with_btcpay_modal_invoice.png)

![BTCPay Server shopify step 35](./img/Shopify/payment_invoice.png)

![BTCPay Server shopify step 36](./img/Shopify/paid_invoice.png)

![BTCPay Server shopify step 37](./img/Shopify/paid_invoice_btcpay.png)

![BTCPay Server shopify step 38](./img/Shopify/invoice_payment_details.png)


Feel free to join our support channel over at [Mattermost](https://chat.btcpayserver.org/) or [Telegram](https://t.me/btcpayserver) if you need help or have any further questions.
