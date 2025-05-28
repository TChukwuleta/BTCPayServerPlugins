force-app/main/default/
├── classes/
│   ├── BTCPayServerService.cls              # API service for BTCPay calls
│   ├── BTCPayServerService.cls-meta.xml
│   ├── BitcoinPaymentService.cls            # Business logic for payments
│   ├── BitcoinPaymentService.cls-meta.xml
│   ├── BTCPayPaymentUpdateAPI.cls           # REST API for plugin callbacks
│   ├── BTCPayPaymentUpdateAPI.cls-meta.xml
│   ├── BTCPayServerServiceTest.cls          # Test classes
│   ├── BTCPayServerServiceTest.cls-meta.xml
│   ├── BitcoinPaymentServiceTest.cls
│   └── BitcoinPaymentServiceTest.cls-meta.xml
│
├── objects/
│   ├── BTCPay_Configuration__c/
│   │   ├── BTCPay_Configuration__c.object-meta.xml
│   │   └── fields/
│   │       ├── API_Key__c.field-meta.xml
│   │       ├── Server_URL__c.field-meta.xml
│   │       └── Store_ID__c.field-meta.xml
│   │
│   ├── BTCPay_Transaction__c/
│   │   ├── BTCPay_Transaction__c.object-meta.xml
│   │   └── fields/
│   │       ├── Amount__c.field-meta.xml
│   │       ├── Invoice_ID__c.field-meta.xml
│   │       ├── Status__c.field-meta.xml
│   │       ├── Opportunity__c.field-meta.xml
│   │       ├── Quote__c.field-meta.xml
│   │       ├── Payment_URL__c.field-meta.xml
│   │       ├── Currency__c.field-meta.xml
│   │       ├── Created_Date__c.field-meta.xml
│   │       └── Expiration_Date__c.field-meta.xml
│   │
│   ├── Opportunity/
│   │   └── fields/
│   │       ├── Bitcoin_Payment_Status__c.field-meta.xml
│   │       ├── Bitcoin_Transaction_ID__c.field-meta.xml
│   │       └── Bitcoin_Paid_Date__c.field-meta.xml
│   │
│   └── Quote/
│       └── fields/
│           ├── Bitcoin_Payment_Status__c.field-meta.xml
│           ├── Bitcoin_Transaction_ID__c.field-meta.xml
│           └── Bitcoin_Paid_Date__c.field-meta.xml
│
├── lwc/
│   └── btcpayPayment/
│       ├── btcpayPayment.html
│       ├── btcpayPayment.js
│       ├── btcpayPayment.js-meta.xml
│       └── btcpayPayment.css
│
├── flexipages/
│   ├── Opportunity_Record_Page.flexipage-meta.xml
│   └── Quote_Record_Page.flexipage-meta.xml
│
├── permissionsets/
│   └── BTCPay_Integration_Admin.permissionset-meta.xml
│
└── staticresources/
    └── btcpay_icons.resource-meta.xml