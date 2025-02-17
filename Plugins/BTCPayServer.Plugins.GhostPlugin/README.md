# Accept Bitcoin on Your Ghost Blog Today 

Tired of relying on Stripe for your Ghost platform and want to try something new? Do you want to receive payment from your Ghost blog in Bitcoin, of course without an 
fees and thrid party?

Introducing BTCPay Server for Ghost – a plugin that allows you accept bitcoin payments directly on your blog from customers with no fee.

Monetizing your content should be simple, and free from third-party fees. Our integration with Ghost allows you connect your self-hosted BTCPay Server with your 
[Ghost blog](https://ghost.org/), enabling you accept Bitcoin payments swiftly and securely.

With this plugin, you're not just adding a payment gateway;  you're embracing a transparent, and secure way to monetize your content which also keeps you in full control of your funds.

Whether you’re receiving donations, selling tiered memberships, or offering event tickets, this integration ensures seamless Bitcoin transactions without third-party fees.


🔹 Seamless Integration – Connect your Ghost blog to BTCPay Server in just a few clicks.

🔹 Zero Fees, Full Control – No third-party processing, just direct Bitcoin payments to your wallet.

🔹 Perfect for Creators – Monetize your blog with Bitcoin-based memberships and content access.

🔹 Event Ticketing Made Easy – Sell tickets for your events, with instant Bitcoin payments.

💡 Ready to power your blog with Bitcoin? Set up your BTCPay Server and start accepting payments



## Prerequisites:

Before diving into the setup process, ensure you have the following:

- [A Ghost account](https://ghost.org/)
- BTCPay Server - [self-hosted](Deployment.md) or run by a [third-party host](/Deployment/ThirdPartyHosting.md) v2.0.0 or later.
- [Created BTCPay Server store](CreateStore.md) with [wallet set up](WalletSetup.md)



## Setting up BTCPay Server with Ghost

1. Login to your BTCPay Server instance, Plugins > Manage Plugins

2. Search for and install BTCPay Server plugin for Ghost

3. Once installed you should see Ghost, included in the side nav, under plugins

4. Log in to your Ghost Admin Panel on a new tab

5. Navigate to Settings > Integrations. Click "Add custom integration" and name it e.g "BTCPay Server"

6. Save the integration, another screen would pop out showing the required Ghost credentials needed by the plugin.

7. Open up your BTCPay Server instance, and the Ghost plugin page.

8. Copy your API Url (without the https://), Content API key, and Admin API Key from Ghost credential page into their respective input fields in your plugin page.

9. For the username/email and password fields, enter your Ghost admin email address, and password.

10. First save your Ghost credetials on Ghost admin portal (You can input a description before saving it if you want to) and close the pop up.

11. Now save the credetial details on BTCPay Plugin. If all data is correct, it should save successfully.
	

![BTCPay Server Ghost img 1](./img/Ghost/Add_Custom_Integration_View.png)


![BTCPay Server Ghost img 2](./img/Ghost/Custom_Integration_Name.png)   


![BTCPay Server Ghost img 3](./img/Ghost/Custom_Integration_Credentials.png)


![BTCPay Server Ghost img 4](./img/Ghost/Ghost_BTCPay_Credential_View.png)


![BTCPay Server Ghost img 5](./img/Ghost/Ghost_BTCPay_Credential_Saved_1.png)   


![BTCPay Server Ghost img 6](./img/Ghost/Ghost_BTCPay_Credential_Saved_2.png)



If you got here without any hassle, congratulations on successfully installing the plugin. 

You can discontinue the plugin by clicking on the stop Ghost calls button, and also deleting the custom integration that you created.

In case you encountered any issue you can report them (Put directory to the section for opening an issue)


## 🎯 What can I do with the BTCPay-Ghost plugin?

Congratulations once again on installing your plugin successfully. Whether you’re a blogger, a business owner, or an event organizer, 

the Ghost - BTCPay Server plugin unlocks new revenue streams.

1. Accept Donations – Allow readers support your work with with donations in Bitcoin.

2. Event Ticketing - Sell tickets for virtual (workshops, consultation sessions and webinars) or in-person events using Bitcoin

3. Tiered Membership Creation and Subscriptions - Offer exclusive content to members and subscribers in a tier.

Each use case comes with an easy setup guide to help you get started quickly. Let’s dive into the details.


### Receiving Donations on Ghost through BTCPay Server

With BTCPay Server, you can accept Bitcoin donations directly on your Ghost blog — no intermediaries, no processing fees, just a seamless way for your audience to support your work.

Whether you're a blogger, journalist, or content creator, adding a Bitcoin donation option gives your supporters an easy way to contribute, while ensuring you maintain full control over your funds.


#### Steps on how to receive donations on Ghost via BTCPay Server.

1. Go to your BTCPay Server Ghost plugin, scroll down the page, you'd see donation URL, copy the URL.

2. Open your ghost admin page where you want to receive donations. You may be open to receiving donations on every content page of your Ghost blog, or on a single page dedicated to donations. Which ever you choose, 
   accepting donations is pretty straightforward. Go to the editor of your donation page (Or any page). Add a new button with a title of your choice e.g Buy me a coffee in Bitcoin, for the Bitcoin URL, paste the
   Donation URL you copied, and paste it there. Save and publish your page

3. When you go to the URL of the page, you should see the button now, click on the button, and a QR code would be displayed on the screen. Users can then scan this QR and support 
   you with any amount they want to. 
   
4. If you check your BTCPay Server invoice, you should see your new donation, received in your wallet. Voila!!!


![BTCPay Server Ghost img 7](./img/Ghost/Donation_Url.png)


![BTCPay Server Ghost img 8](./img/Ghost/Ghost_Donation_Page.png)


![BTCPay Server Ghost img 9](./img/Ghost/Donation_Page.png)


![BTCPay Server Ghost img 10](./img/Ghost/Donation_Invoice.png)


![BTCPay Server Ghost img 11](./img/Ghost/Paid_Donation.png)




### Event management using BTCPay Server

If you are an event organizers, conference hosts, or community managers, you can now create and manage events on your Ghost platform that accept payments on 

Bitcoin using BTCPay Server. With this you can create an event, set ticket pricing and available quantities, allow attendees purchase their tickets using Bitcoin.

Manage ticket payments e.t.c.


#### Steps on how to setup Events on Ghost via BTCPay Server.


1. Click on your BTCPay Server Ghost plugin, you'd see a dropdown of available use cases, select 'Ghost Event'.

2. Click on Create event on the top right.

3. Fill out all required event information such as title, Link (online event) or Address (Physical event), Event logo, Description, Ticket fee and currency

	Event date, number of ticket for sale (if not an unlimited ticket event), the email subject and body that would be sent to customers on purchase of every

	ticket.

	N.B: It is important that you have configured your email service in server settings, this is needed so that customers get all required information needed in their email.

4. Once done, click on create. This should create the event successfully, and you should see it populated in the list of events available.



![BTCPay Server Ghost img 12](./img/Ghost/Ghost_Event_View.png)


![BTCPay Server Ghost img 13](./img/Ghost/Create_Ghost_Event_1.png)


![BTCPay Server Ghost img 14](./img/Ghost/Create_Ghost_Event_2.png)


![BTCPay Server Ghost img 15](./img/Ghost/Create_Ghost_Event_3.png)


![BTCPay Server Ghost img 16](./img/Ghost/Created_Ghost_Event_List_View.png)








Integrating BTCPay Server with Ghost Commerce to accept Bitcoin payments provides a variety of use cases, depending on your goals and the type of e-commerce or content-based transactions you're running. 

Here are some key use cases:

1. Subscription Payments for Premium Content
If you're offering premium content or memberships on your Ghost site (like articles, tutorials, or videos), you can allow users to pay for subscriptions using Bitcoin. This is especially appealing to users who prefer decentralized and censorship-resistant payment methods.
2. One-Time Purchases
Enable one-time payments for digital products, merchandise, or services through Ghost's commerce platform using Bitcoin. For instance, eBooks, online courses, or physical items.
3. Donations or Crowdfunding
Set up a seamless way to accept donations in Bitcoin for specific campaigns or ongoing funding. This can be particularly useful for creators, nonprofits, or activists.


