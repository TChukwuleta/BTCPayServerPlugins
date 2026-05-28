# BTCPay Server - Server Alert Plugin

A BTCPay Server plugin that gives server administrators two tools in one: broadcast announcements to users and store owners, and monitor server and store health with automatic alerts when something needs attention.

## Features

### Announcements
Broadcast server-wide messages to all store owners and users directly from BTCPay Server. Whether it is scheduled maintenance, a service outage, or an
important update, everyone on your server gets notified instantly via bell notifications and email.

### Health Monitor
Automatically monitor your server and store infrastructure on a schedule. Get alerted the moment something goes wrong and also when it recovers without having to check the dashboard manually.

**Server-level checks:**
- Bitcoin node connectivity and sync status

**Store-level checks:**
- Approved payouts sitting unprocessed beyond a configurable threshold
- Lightning node offline or unreachable
- Lightning inbound capacity below a configurable threshold

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

### Managing Alerts
From the Server Alerts list you can:

Edit — update the title, message, severity, or email scope
Resend — re-dispatch bell and email notifications for an existing alert
Delete — permanently remove an alert

### Server Monitor

Navigate to Server Settings -> Server Alerts -> Health Monitor.

Enable the monitor and configure:

- Bitcoin Node: checks whether the node is online and fully synced. Alerts admins if the node goes offline and sends a recovery notification when it comes back.
- Alert Delivery: bell notification only, email only, or both

The monitor runs on a background schedule. Alerts fire once when a problem is detected and once again when it resolves. No repeat spam between state changes.

### Store Monitor

Click on the store health monitor on the left navigation.

Enable the monitor and configure:

- Unprocessed payouts: alerts when approved payouts have not been sent within a configurable number of hours
- Lightning node offline: alerts when the store's Lightning node is unreachable or not responding
- Low inbound capacity: alerts when the percentage of channel capacity available for incoming payments falls below a configurable threshold.
- Channel close detection: compares channel state between checks. If a channel disappears it may indicate a force close. You will be notified to investigate your Lightning node.
- Alert Delivery: bell notification only, email only, or both

Store alerts go to the store owner's email and bell notifications. Each store owner configures their own thresholds independently.


## Contribute

This BTCPay Server plugin is built and maintained entirely by contributors around the internet. We welcome and appreciate new contributions.

Do you notice any errors or bug? are you having issues with using the plugin? would you like to request a feature? or are you generally looking to support the project and plugin please [create an issue](https://github.com/TChukwuleta/BTCPayServerPlugins/issues/new)

Feel free to join our support channel over at [https://chat.btcpayserver.org/](https://chat.btcpayserver.org/) or [https://t.me/btcpayserver](https://t.me/btcpayserver) if you need help or have any further questions.

