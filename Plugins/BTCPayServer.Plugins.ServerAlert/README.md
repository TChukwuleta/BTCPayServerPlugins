# BTCPay Server - Server Alert Plugin

Broadcast server-wide announcements to all your store owners and users directly from BTCPay Server. Whether it's scheduled maintenance, a service outage, or an important update 
this plugin ensures everyone on your server gets notified instantly via bell notifications and email.

## What you need?

- A running BTCPay Server instance (self-hosted or hosted by a third party)
- Server administrator access
- SMTP configured in Server settings (optional, required for email notifications)



## Installation

1. On the left navigation of BTCPay Server UI click Manage Plugins, search for **Server Alert**
2. Install the plugin, and if BTCPay Server requests that you restart the instance, go ahead and restart.
3. Once the plugin is installed, you should see Server Alerts under Server Settings in the left navigation.

### Sending alert notifications

Navigate to `Server Settings` > `Server Alerts` click `Send Alert` button. Fill in:

Notification Title — a short headline (e.g. Scheduled Maintenance)

Notification Message — the full message in plain text

Severity — Info, Warning, or Critical


Under Email Notifications, choose who receives an email:

None — bell notification only
All stores — owners of every store on the server
All users — every registered user
Admins only — server administrators only
Selected store owners — pick specific stores from a searchable list
Custom email list — enter any email addresses manually


Click Send


This plugin uses your server's existing SMTP configuration for email notifications. To enable email notifications, make sure SMTP is configured under `Server Settings` > `Emails`.

#### Managing Alerts
From the Server Alerts list you can:

Edit — update the title, message, severity, or email scope
Resend — re-dispatch bell and email notifications for an existing alert
Delete — permanently remove an alert

## Contribute

This BTCPay Server plugin is built and maintained entirely by contributors around the internet. We welcome and appreciate new contributions.

Do you notice any errors or bug? are you having issues with using the plugin? would you like to request a feature? or are you generally looking to support the project and plugin please [create an issue](https://github.com/TChukwuleta/BTCPayServerPlugins/issues/new)

Feel free to join our support channel over at [https://chat.btcpayserver.org/](https://chat.btcpayserver.org/) or [https://t.me/btcpayserver](https://t.me/btcpayserver) if you need help or have any further questions.

