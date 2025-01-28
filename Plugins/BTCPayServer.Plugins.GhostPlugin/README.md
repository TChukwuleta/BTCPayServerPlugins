Use Cases:
Membership Payments:
Allow Ghost users to pay for memberships via Bitcoin using BTCPay Server.
Automatically update the membership status in Ghost once payment is confirmed.
Subscription Renewals:
Automate Bitcoin payments for recurring memberships (handled manually since BTCPay doesn’t natively support subscriptions... until subscription plugin is complete).





Integrating BTCPay Server with Ghost Commerce to accept Bitcoin payments provides a variety of use cases, depending on your goals and the type of e-commerce or content-based transactions you're running. Here are some key use cases:

1. Subscription Payments for Premium Content
If you're offering premium content or memberships on your Ghost site (like articles, tutorials, or videos), you can allow users to pay for subscriptions using Bitcoin. This is especially appealing to users who prefer decentralized and censorship-resistant payment methods.
2. One-Time Purchases
Enable one-time payments for digital products, merchandise, or services through Ghost's commerce platform using Bitcoin. For instance, eBooks, online courses, or physical items.
3. Donations or Crowdfunding
Set up a seamless way to accept donations in Bitcoin for specific campaigns or ongoing funding. This can be particularly useful for creators, nonprofits, or activists.


## Receiving Donations on Ghost through BTCPay Server
Together with the ability to  fiat donations on your Ghost website, with BTCPay Server, you can now also receive donations in Bitcoin.
Follow the guide to get started.

### Steps on how to receive donations on Ghost via BTCPay Server.
1. Go to the editor of your donation page. (You can create a new page for Donation on your admin panel if you do not have one.), Add a button, with a Title of your choice e.g Donate Bitcoin. 
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