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

1. In Shopify, click on `Apps >` in the left sidebar
2. On the modal popped up, click on `App and sales channel settings`
3. From the page displayed, click on `Develop apps` button
4. If prompted, click on `Allow custom app development`
5. `Create an app` and name it, e.g. BTCPay Server
6. On the app page, in `Overview` tab, click on the `Configure Admin API scopes`
7. In the filter admin access scopes type in `Orders`
8. In `Orders` enable `read_orders` and `write_orders` and then click `Save`
9. Click on the API credential tab, and then click on the `Install App` in the top right corner and when pop-up window appears click `Install`
10. Reveal `Admin API access token` and `copy` it
11. In your BTCPay Server, go to your store and on the left sidebar click on `Shopify`
12. In the first field, `Shop name` enter the subdomain of your Shopify store e.g. SOME_ID.myshopify.com then enter SOME_ID
13. In the last field, `Admin API access token` paste the `Admin API access token` you just copied from Shopify
14. In the second field, `API key` paste the `API key` from Shopify - which you can find at the bottom of the same page where you copied the Admin API access token. Do the same for the third field, paste the `API Secret` from Shopify and paste in the `API Secret` filed
15. You can decide to edit the payment method description text. This basically defines the text that the user sees when the invoice loads on shopify.
16. Click `Save` on BTCPay Shopify settings page 
17. BTCPay then validates the credentials, and once validated, creates an create order webhook, and finally saves the credentials.
18. Now click on `Payments` in the left sidebar, scroll down to "Manual payment methods", click on `(+) Manual payment method` and select `Create custom payment method` on the dropdown.
19. In `Custom payment method name` fill in `Bitcoin with BTCPay Server`, optionally you can fill in other fields, but it's not required.
	However you would need to inform your customers that payment with Bitcoin comes on the next screen (Thank you) after checkout, so you would need to inform them in the `Additional details` of the `Manual payment method`.
	The payment option can have a delay between 2 - 10 seconds on the Thank you page, before it is displayed so this also needs to be communicated with the customers.
    	
20. Hit `Activate` and you've set up Shopify and BTCPay Server payment method successfully.

:::tip
"Custom Payment method name" **must** contain at least one of the following words (case-insensitive): `bitcoin`, `btcpayserver`, `btcpay server` or `btc` to work.
:::

Below are step by step visuals describing the process outlined above. The steps continue after the images.

![BTCPay Server shopify step 1](./img/Shopify/step_1.png)

![BTCPay Server shopify step 2](./img/Shopify/step_2.png)

![BTCPay Server shopify step 3](./img/Shopify/step_3.png)

![BTCPay Server shopify step 4](./img/Shopify/step_4.png)

![BTCPay Server shopify step 5](./img/Shopify/step_5.png)

![BTCPay Server shopify step 6](./img/Shopify/step_6.png)

![BTCPay Server shopify step 7](./img/Shopify/step_7.png)

![BTCPay Server shopify step 8](./img/Shopify/step_8.png)

![BTCPay Server shopify step 9](./img/Shopify/step_9.png)

![BTCPay Server shopify step 10](./img/Shopify/step_10.png)

![BTCPay Server shopify step 7](https://github.com/user-attachments/assets/6c00ef02-2d13-46d3-ad5e-3fee90f32394)

![BTCPay Server shopify step 8](https://github.com/user-attachments/assets/71c1cdcb-75d5-463c-8d3a-1ba4a8d19122)

![BTCPay Server shopify step 9](https://github.com/user-attachments/assets/f5a89262-26f4-4178-bc36-00656bb0d689)

![BTCPay Server shopify step 10](https://github.com/user-attachments/assets/daddffe9-6460-456c-aaf3-252e24cbd8ef)

![BTCPay Server shopify step 11](https://github.com/user-attachments/assets/089a03ca-d461-4c0c-81e8-0a5edb12e843)

![BTCPay Server shopify step 12](https://github.com/user-attachments/assets/8a3f8f99-8dd7-4b59-9e32-9d53905db373)

![BTCPay Server shopify step 13](https://github.com/user-attachments/assets/90f7f4ff-739b-4b45-8d10-f3af497fe77e)


20. Now we need to install the BTCPay - Shopify app. Visit the [Shopify app store](https://apps.shopify.com/) search for and install the BTCPay Server plugin application
21. Once installed, you can go to your dashboard, click on `Apps >` in the left sidebar, then you would see the list of installed application, including the just installed BTCPay plugin app. 
22. Select the BTCPay app, and in the view input your BTCPay Server domain url (e.g btcpay.example.com) and the storeId to which your shopify plugin is connected on the BTCPay instance

![BTCPay Server shopify step 14](https://github.com/user-attachments/assets/40469ad5-6545-4231-85ff-c9e35e729f76)

![BTCPay Server shopify step 15](https://github.com/user-attachments/assets/f498488c-e0e6-4ac6-8cab-fdb3db5a2746)

23. In your shopify dashboard, click on `Settings`, whch is located on the bottom of the left nav panel, select `Checkout` and then `Customize`
24. In the editor change the selected page to the Thank you page

![BTCPay Server shopify step 16](https://github.com/user-attachments/assets/62c850db-dfaf-46c2-b7a3-4d911e96bd15)

![BTCPay Server shopify step 17](https://github.com/user-attachments/assets/a8651278-0272-47bd-b685-a55eb41de6c9)

![BTCPay Server shopify step 18](https://github.com/user-attachments/assets/cebb68c7-9593-4c52-ad49-89d2858f155f)

![BTCPay Server shopify step 19](https://github.com/user-attachments/assets/cde0f703-dd72-45c0-9746-e9462a3b77ab)


23. Click on `Sections` icon on the left panel > `Add app block` on the bottom of the section tab
24. Select the BTCPay Server extensions.
25. Click `Save` button on the top right corner of the page to save the changes


![BTCPay Server shopify step 20](https://github.com/user-attachments/assets/4756be6d-513d-41c0-9e28-4c5b1086cd84)

![BTCPay Server shopify step 21](https://github.com/user-attachments/assets/bdc64c5e-af81-417e-b247-85c610687fb7)

![BTCPay Server shopify step 22](https://github.com/user-attachments/assets/19cf8823-23a5-4c40-8a1d-8db56ae2b54b)


Demo Checkout flow after everything is set up:

![BTCPay Server shopify step 15](./img/Shopify/payment_option.png)

![BTCPay Server shopify step 16](./img/Shopify/complete_payment.png)

![BTCPay Server shopify step 17](./img/Shopify/pay_with_btcpay_modal.png)

![BTCPay Server shopify step 18](./img/Shopify/pay_with_btcpay_modal_invoice.png)

![BTCPay Server shopify step 19](./img/Shopify/payment_invoice.png)

![BTCPay Server shopify step 20](./img/Shopify/paid_invoice.png)

![BTCPay Server shopify step 21](./img/Shopify/paid_invoice_btcpay.png)

![BTCPay Server shopify step 21](./img/Shopify/invoice_payment_details.png)


Feel free to join our support channel over at [Mattermost](https://chat.btcpayserver.org/) or [Telegram](https://t.me/btcpayserver) if you need help or have any further questions.