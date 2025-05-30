﻿# Accept Bitcoin on Your Ghost Blog Today 

Introducing BTCPay Server for Ghost – a simple way to accept payments from your customers via Bitcoin directly on your [Ghost blog](https://ghost.org/).
Monetizing your content just got easier. With this integration, you have full control over your earnings—no middlemen, no extra charges.


## 🎯 What can I do with the BTCPay-Ghost integration?

1. Accept Donations – Allow readers support your work with donations in Bitcoin.

2. Paywall Content – Restrict access to premium articles, videos, or other content, unlocking it through Bitcoin payments.

3. Tiered Membership Creation and Subscriptions - Offer exclusive content to members and subscribers in a tier.


Ready to take your blog to the next level? Set up BTCPay Server and start accepting Bitcoin payments today 🚀

## Prerequisites:

Before diving into the setup process, ensure you have the following:

- [A Ghost account](https://ghost.org/) or [self-hosted Ghost instance](https://ghost.org/docs/install/).
- BTCPay Server - [self-hosted](Deployment.md) or run by a [third-party host](/Deployment/ThirdPartyHosting.md) v2.0.7 or later.
- [Created BTCPay Server store](CreateStore.md) with [wallet set up](WalletSetup.md)


## Setting up BTCPay Server with Ghost

1. Login to your BTCPay Server instance, Plugins > Manage Plugins

2. Search for and install BTCPay Server plugin for Ghost

3. Once installed you should see Ghost, included in the side nav of your instance under plugins section

4. Log in to your Ghost Admin Panel on a new tab

5. Navigate to Settings (cogwheel at the bottom), in section "Advanced" click on "Integrations". Click "Add custom integration" and name it e.g "BTCPay Server"
   
   ![BTCPay Server Ghost img 1](./img/Ghost/Add_Custom_Integration_View.png)   
   ![BTCPay Server Ghost img 2](./img/Ghost/Custom_Integration_Name.png)   

6. A modal will show the required Ghost credentials needed by the plugin
   
   ![BTCPay Server Ghost img 3](./img/Ghost/Custom_Integration_Credentials.png)

7. Click on "Save" to save your credentials, you can also put in a description before saving it.

8. Open up your BTCPay Server instance, make sure you selected the right store and open the Ghost plugin page.

9. Copy your API Url which is the same as your site domain URL (without the https://), Content API key, and Admin API Key from Ghost credential page into their respective input fields in your plugin page.

10. For the Webhook Secret you can enter a secret on your own or if left empty it will auto-generate one. This will only be used for the membership functionality.

11. Now save the credentials details on BTCPay Server Ghost Plugin.

![BTCPay Server Ghost img 5](./img/Ghost/Ghost_BTCPay_Credential_Saved_1.png)   

![BTCPay Server Ghost img 6](./img/Ghost/Ghost_BTCPay_Credential_Saved_2.png)

12. While on the plugin page, copy the script snippet from the bottom of the page. Open your Ghost admin portal: Settings >> Code Injection, open code injection and under site header paste the script url, save and close
 
![BTCPay Server Ghost img 60](./img/Ghost/GhostPluginWithScript.png)

![BTCPay Server Ghost img 36](./img/Ghost/Code_Injection_Setting_View.png)   

Congratulations on successfully installing the plugin. 

In case you encountered any issue you can report them [here](https://github.com/TChukwuleta/BTCPayServerPlugins/issues) (prefix "Ghost" to title of the issue)


## Receiving Donations on Ghost through BTCPay Server

1. Go to your BTCPay Server Ghost plugin, scroll down the page, you'd see "Donation Url", copy the URL.

2. Open your ghost admin page where you want to receive donations. You may be open to receiving donations on every content page of your Ghost blog, or on a single page dedicated to donations. Which ever you choose, 
   accepting donations is pretty straightforward. Go to the editor of your donation page (or any page). Add a new button (by clicking the (+) sign) with a title of your choice e.g Buy me a coffee in Bitcoin, for the Bitcoin URL, paste the
   Donation URL you copied, and paste it there. Save and publish your page.

3. When you go to the URL of the page, you should see the button now, click on the button, and a QR code would be displayed on the screen. Users can then scan this QR and support 
   you with any amount they want to. 
   
4. If you check your BTCPay Server invoice, you should see your new donation, received in your wallet. Voila!!!


![BTCPay Server Ghost img 7](./img/Ghost/Donation_Url.png)


![BTCPay Server Ghost img 8](./img/Ghost/Ghost_Donation_Page.png)


![BTCPay Server Ghost img 9](./img/Ghost/Donation_Page.png)


![BTCPay Server Ghost img 10](./img/Ghost/Donation_Invoice.png)


![BTCPay Server Ghost img 11](./img/Ghost/Paid_Donation.png)



## Payment Paywall on Ghost

With BTCPay Server - Ghost plugin, you can now hide premium content on your blog post until the user makes a successful payment. This guide explains how to implement a Bitcoin paywall using BTCPay Server. 

Please note that this is not fully protecting your content but just hiding it until payment is made. Technical users can bypass this paywall by inspecting the page source.

1. Ensure you have copied the script url into the site header. If you haven't, go to your BTCPay Server Ghost plugin, scroll down the page, copy the paywall script url.  

2. Head over to your Ghost admin portal, Settings >> Code Injection >> Add custom code, click on the "Open" button.
   ![BTCPay Server Ghost img 36](./img/Ghost/Code_Injection_Setting_View.png)   

3. Under the site header paste the script url, click on "save" and close the modal.
   ![BTCPay Server Ghost img 37](./img/Ghost/Code_Injection_Script_view.png)   

4. Now that you have the script injected, head over to the post/page editor where you want to include your paywall. Click on the plus icon to add an item, select HTML, and in the input field paste the following code
   ![BTCPay Server Ghost img 38](./img/Ghost/Add_Html_To_Post.png)

```
    <btcpay-gated-content data-price="0">
        <h2>Exclusive Content 🎉</h2>
        <p>You have unlocked this premium content </p>
        <button>Enjoy!</button>
    </btcpay-gated-content>
```

These is a custom HTML tag. You can customize your premium content using HTML tags, just ensure it is wrapped inside the custom custom tag (<btcpay-gated-content data-price="4"></btcpay-gated-content>)

Finally replace the `data-price` value from 0 to whatever amount you want to sell your content for i.e. replace the 0 in `data-price="2"` with any amount.

P.S.: The currency associated with the amount is the default currency set for your BTCPay Server store

P.S: Also note that as for now, you can only customize one paywall per page.

If you are a technical person and good with styling and customization, further customization can be done to the elements. 

![BTCPay Server Ghost img 39](./img/Ghost/Html_Content.png)


![BTCPay Server Ghost img 39a](./img/Ghost/Html_Content1.png)

In your editor the premium content isn't hidden, allowing full display of your page. Not to worry as it is hidden in the post/page URL.


Once done, save your page/post, open the url to the post and proceed to make a payment.

![BTCPay Server Ghost img 40](./img/Ghost/Paywall_View_One.png)


![BTCPay Server Ghost img 41](./img/Ghost/Paywall_View_Two.png)

![BTCPay Server Ghost img 42](./img/Ghost/Paywall_View_Three.png)

![BTCPay Server Ghost img 43](./img/Ghost/Paywall_View_Four.png)

![BTCPay Server Ghost img 44](./img/Ghost/Paywall_View_Five.png)

One final note, this content would be available to the user, while his browser data is still on, and would stop being once the data is cleared. It would be good to inform users to save the content once revealed.


## Membership and subscription BTCPay Server

Ghost doesn’t provide a direct way to integrate custom payment providers. Instead, it relies on a private API for payment processing, which is currently built around Stripe as the default processor.

Why does this matter? While BTCPay Server can handle subscriptions and send notifications to the users, it cannot automatically deactivate or delete members in Ghost once a subscription ends. 

This means subscription management remains the admin’s responsibility. However, you can configure the Ghost plugin to send automated notifications to the customer when a subscription is nearing expiration, allowing for timely action.


### Setting up membership subscription via BTCPay Server


1. Head to your BTCPay Server Ghost plugin, scroll down on the page, copy the 'Membership subscription Url'.

   ![BTCPay Server Ghost img 21](./img/Ghost/Membership_Url_Ghost_View.png)   

2. Head over to your Ghost admin portal, click on Settings >> Portal Settings, click on the 'Customize' button.

   ![BTCPay Server Ghost img 22](./img/Ghost/Setup_Membership_Portal.png)

   ![BTCPay Server Ghost img 23](./img/Ghost/Setup_Membership_Portal.png)   

3. Edit the field at 'Display notice at signup' in the right sidebar and to include instructions on how to signup with Bitcoin as payment option, directing users to use the link. For instance: "Sign up with Bitcoin using this link". **Important**: Mark "this link" with your mouse and add a link with the URL copied from the plugin.
   ![BTCPay Server Ghost img 24](./img/Ghost/Membership_Portal_Setup.png)

4. Save the changes and the close to the portal. You can navigate to the signup page to view your changes. It should look similar to this:   
   ![Membership_signup.png](./img/Ghost/Membership_signup.png)

5. Before we proceed to testing it out, we need to configure webhooks for our integrations, these webhook would alert the plugin for when member's data are updated or deleted. 

6. Go to your Ghost plugin view in BTCPay Server, and note down/copy the 'Webhook Secret' and 'Webhook Url' values.

7. In your Ghost admin portal, navigate to Settings >> Integrations. Select the custom integration you earlier created.

8. Click on 'Add webhook'. Enter a name for the webhook, for the event, select 'Member Updated', paste the URL copied into the 'Target URL' and secret into 'Secret' fields. Click on "Add".
   ![BTCPay Server Ghost img 25](./img/Ghost/Member_Update_Webhook.png)

9. Create another webhook, this time the event would be 'Member Deleted'. Use the same URL and Secret for the 'Target URL' and 'Secret' and click on "Add" again.
   ![BTCPay Server Ghost img 26](./img/Ghost/Member_Delete_Webhook.png)

10. Once you can confirm that both your webhooks has been populated, click on 'Save' and close the modal.
	![BTCPay Server Ghost img 27](./img/Ghost/Member_Credentials_Webhook_View.png)

### Testing the membership subscription

1. Users can then proceed to create membership and pay via BTCPay Server. When a user clicks on the link, he is redirected to a page where he can select his tier.
   ![Membership_signup.png](./img/Ghost/Membership_signup.png)

2. The user is prompted to enter their name and email, and also prompted to select their tier to which they need to subscribe to.
   ![BTCPay Server Ghost img 28](./img/Ghost/Membership_Create.png)

3. Once he is done filling his details, he then clicks on subscribe which would show an invoice containing his first payment to subscribe to being a member.

4. Once the invoice has been paid, a member account is created by the user on the Ghost platform, the user can then proceed to log in.


### Managing Members and Subscriptions

1. The admin can view all subscribed members by heading over to the BTCPay Server >> Ghost plugin >> Ghost members.
   ![BTCPay Server Ghost img 29](./img/Ghost/Ghost_Members_List.png)   
2. The admin can see all members, members with active subscription, members whose subscription would soon expire, and members whose subscription has expired.
   ![BTCPay Server Ghost img 30](./img/Ghost/Ghost_Members_Active_List.png)   

3. A member subscription is tagged as 'soon to expire' when the subscription ends a few days away from the end date. The days specification can be configured in the Settings.
   ![BTCPay Server Ghost img 31](./img/Ghost/Ghost_Members_SoonToExpire_List.png)   

4. Once a member subscription is expired you can see that in the "Expired Subscription" tab.
   ![BTCPay Server Ghost img 32](./img/Ghost/Ghost_Members_Expired_List.png)   

5. Once a member subscription is about to expire, there is a green notification that appears on the Ghost plugin side navigation. This is here to notify the admin. 
   The admin can then proceed to notify the members, before the member's subscription expires.
   ![BTCPay Server Ghost img 33](./img/Ghost/Ghost_Alert_Notification.png)

6. The admin can also view all payments a particular associated with the membership by clicking on the "View" link.
   ![Ghost_Members_Payments_List.png](./img/Ghost/Ghost_Members_Payments_List.png)

### Ghost plugin settings

As earlier mentioned, an admin can configure settings for his Ghost plugin. 

- The admin can enable or disable automated email reminders for expiring and expired subscriptions. This would require the admin to have configured email SMTP in admin settings.
- The admin can also set the time frame to begin notification for member subscription.
![BTCPay Server Ghost img 34](./img/Ghost/Ghost_Settings.png)


## Uninstalling
You can discontinue the plugin by clicking on the "Stop Ghost calls and clear credentials" button, and also deleting the custom integration that you created.


## Contribute

BTCPay Server is built and maintained entirely by contributors around the internet. We welcome and appreciate new contributions.

Do you notice any errors or bug? are you having issues with using the plugin? would you like to request a feature? or are you generally looking to support the project and plugin please [create an issue](https://github.com/TChukwuleta/BTCPayServerPlugins/issues/new)

Feel free to join our support channel over at [https://chat.btcpayserver.org/](https://chat.btcpayserver.org/) or [https://t.me/btcpayserver](https://t.me/btcpayserver) if you need help or have any further questions.
