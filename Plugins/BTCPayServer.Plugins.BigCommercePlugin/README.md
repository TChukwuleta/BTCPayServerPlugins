# Big Commerce plugin for BTCPayServer

This plugin allows you to integrate your [BigCommerce](https://bigcommerce.com/) application with BTCPay Server. 

With this you have the option of accepting Bitcoin payments for your store.

## Installation

1. Install the plugin from Plugins=>Add New=> BigCommerce
2. Restart BTCPay Server
3. Log in to your Big commerce application, and set up an application to be used for receiving bitcoin payment via BTCPay Server account
4. Copy the client Id and the client Secret of the created application
5. Go back to your BTCPay Server, choose the store to integrate with and click on Big Commerce in the navigation. 
6. Enter the Client Id and secret and click create. (P.S: For multiple store integrations, kindly use unique client Id and secret for each store)
7.Once completed, the plugin would then display your auth callback url, load url, and uninstall url..
8. Log back to your big commerce application and update your app with the three url.


## Flow
When a customer goes to the ticket purchase page, they can enter a name and must enter an email. Ticket Tailor requires a full name, so we generate one if not specified.
After the tickets are selected, the customer is redirected to the BTCPay Server checkout page, and a hold for the selected tickets is created to reserve the tickets for this customer. After the payment is sent, the customer is redirected to a custom receipt page where they can see their tickets. Tickets are only issued AFTER an invoice is settled. If an invoice is set to invalid or expired, the hold is deleted and the tickets are released for sale again.


## Additional Configuration

You should configure the [SMTP email settings in the store](https://docs.btcpayserver.org/Notifications/#store-emails) so that users receive the ticket link by email after an invoice is settled.
You're also able to override ticket names, prices and description on the BTCPay Server side.

## Secret Tickets

You can configure a ticket on ticket tailor to require an access code. BTCPay Server allows you to add `?accessCode=XXXX` to the ticket purchase page url to allow customers to view and purchase these secret tickets.
