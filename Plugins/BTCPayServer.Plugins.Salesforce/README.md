If you dont have a salesforce site..

you can follow this guide: https://help.salesforce.com/s/articleView?id=platform.sites_setup_overview.htm&type=5



On getting details:
- your username is your salesforce username
- Your password is your salesforce password
- For the salesforce security tokem.. Log in to your salesforce account, go to profile, click on Settings, and on the left bar click on Reset my security token.. and your security token would be sent to your email.

In Salesforce Setup:

Go to App Manager → New External client App
Enable OAuth Settings
Add scopes: api, refresh_token, offline_access
1. Access Lightning applications (lightning)
2. Perform requests at any time (refresh_token, offline_access)
1. Manage user data via APIs (api)
Enable "Client Credentials Flow" for server-to-server integration



1. Local: These apps are developed and used exclusively within a single Salesforce org. They cannot be distributed to other orgs and are not copied to a new sandbox when you clone or refresh a sandbox. This setting is ideal for apps that are specific to a single organization and do not require sharing or distribution.
Packaged: These apps are designed to be included in second-generation (2GP) managed packages and can be distributed to subscriber orgs. This setting enables the app to be shared across multiple organizations, making it suitable for apps that need to be deployed in various environments or shared with other Salesforce users. Packaged external client apps are copied to sandboxes during refresh or clone operations, ensuring consistency across environments.



1. A custom object needs to be manually created.. Before I can reate the custom fields

Security token:

- Profile (top right) -> Settings -> Reset my security token



Steps: 

- Credentials setup
- Custom objects (Predefined)
- create custom object fields and record
- Deploy the code
- Create new gateway provider 
- Create a named credentials
- Create a payment gateway record



https://developer.salesforce.com/docs/atlas.en-us.apexcode.meta/apexcode/apex_commercepayments_async_adapter_setup.htm