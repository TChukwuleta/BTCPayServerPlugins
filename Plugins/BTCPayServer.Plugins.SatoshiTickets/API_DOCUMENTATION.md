# SatoshiTickets Greenfield API Documentation

## Authentication

All endpoints require a BTCPay Server Greenfield API key passed in the `Authorization` header.

```
Authorization: token YOUR_API_KEY
```

**Required permissions:**
- `btcpay.store.canviewstoresettings` - for all GET (read) endpoints
- `btcpay.store.canmodifystoresettings` - for all POST, PUT, DELETE (write) endpoints

To create an API key: BTCPay Server > Account > Manage Account > API Keys > Generate Key. Select at minimum the permissions `CanViewStoreSettings` and `CanModifyStoreSettings` for the target store.

## Base URL

All endpoints are prefixed with:

```
{server}/api/v1/stores/{storeId}/satoshi-tickets
```

Replace `{server}` with your BTCPay Server URL (e.g. `https://btcpay.example.com`) and `{storeId}` with the target store ID.

## Error Responses

All errors follow the standard Greenfield format:

```json
{
  "code": "error-code-string",
  "message": "Human-readable error description"
}
```

Validation errors (HTTP 422):

```json
[
  { "path": "FieldName", "message": "Validation error message" }
]
```

Common HTTP status codes:
- `200` - Success
- `201` - Created (returned by POST create endpoints)
- `404` - Resource not found
- `422` - Validation error or business rule violation
- `500` - Server error

---

## 1. EVENTS

### 1.1 List Events

```
GET /api/v1/stores/{storeId}/satoshi-tickets/events
```

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `expired` | bool | `false` | If `true`, returns only events where `startDate` is in the past |

**Response: `200 OK`**

```json
[
  {
    "id": "abc123",
    "storeId": "store-id",
    "title": "Bitcoin Conference 2026",
    "description": "Annual Bitcoin meetup",
    "eventType": "Physical",
    "location": "Bratislava, Slovakia",
    "startDate": "2026-06-15T18:00:00Z",
    "endDate": "2026-06-16T23:00:00Z",
    "currency": "EUR",
    "redirectUrl": "https://example.com/thank-you",
    "emailSubject": "Your ticket for {{Title}}",
    "emailBody": "Hello {{Name}}, your ticket...",
    "hasMaximumCapacity": true,
    "maximumEventCapacity": 500,
    "eventState": "Active",
    "eventLogoFileId": "file-abc-123",
    "eventLogoUrl": "https://btcpay.example.com/LocalStorage/file-abc-123.png",
    "purchaseLink": "https://btcpay.example.com/plugins/store-id/ticket/public/event/abc123/summary",
    "ticketsSold": 42,
    "createdAt": "2026-01-15T10:30:00+00:00"
  }
]
```

---

### 1.2 Get Single Event

```
GET /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}
```

**Response: `200 OK`** - Same object structure as in the list above (single object, not array).

**Error: `404`** - `{"code": "event-not-found", "message": "The event was not found"}`

---

### 1.3 Create Event

```
POST /api/v1/stores/{storeId}/satoshi-tickets/events
Content-Type: application/json
```

**Request body:**

```json
{
  "title": "Bitcoin Conference 2026",
  "description": "Annual Bitcoin meetup in Bratislava",
  "eventType": "Physical",
  "location": "Bratislava, Slovakia",
  "startDate": "2026-06-15T18:00:00Z",
  "endDate": "2026-06-16T23:00:00Z",
  "currency": "EUR",
  "redirectUrl": "https://example.com/thank-you",
  "emailSubject": "Your ticket for {{Title}}",
  "emailBody": "Hello {{Name}}, here is your ticket for {{Title}} at {{Location}}.",
  "hasMaximumCapacity": true,
  "maximumEventCapacity": 500
}
```

**Field details:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `title` | string | YES | Event title |
| `description` | string | no | Event description (HTML allowed) |
| `eventType` | string | no | `"Physical"` or `"Virtual"`. Defaults to `"Physical"` |
| `location` | string | no | Event venue / location |
| `startDate` | datetime | YES | ISO 8601 format. Must be in the future |
| `endDate` | datetime | no | ISO 8601. Must be after `startDate` if provided |
| `currency` | string | no | 3-letter currency code (e.g. `"EUR"`, `"USD"`, `"BTC"`). If omitted, uses store default currency |
| `redirectUrl` | string | no | URL to redirect customer after successful payment |
| `emailSubject` | string | no | Email subject template. Supports `{{Title}}`, `{{Location}}`, `{{Name}}`, `{{Email}}` placeholders |
| `emailBody` | string | no | Email body template. Same placeholders as subject |
| `hasMaximumCapacity` | bool | no | Whether event has a total ticket cap |
| `maximumEventCapacity` | int | conditional | Required and must be > 0 if `hasMaximumCapacity` is `true` |
| `eventLogoFileId` | string | no | File ID of an uploaded image (see "Uploading Event Logo" below) |

**Response: `201 Created`** - Returns the created `EventData` object.

**Important:** Events are created in `Disabled` state. You must create at least one ticket type, then call the Toggle endpoint to activate.

**Validation errors (422):**
- `"Title is required"`
- `"Event date cannot be in the past"`
- `"Event end date cannot be before start date"`
- `"Maximum event capacity must be greater than zero when capacity is enabled"`
- `"Invalid event type. Valid values: Virtual, Physical"`

---

### 1.4 Update Event

```
PUT /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}
Content-Type: application/json
```

**Request body:** Same structure as Create Event.

**Response: `200 OK`** - Returns the updated `EventData` object.

**Additional validation (422):**
- `"Maximum capacity is less than the sum of all tiers capacity"` - if reducing capacity below existing ticket type quantities

**Event Logo handling in Update:**
- Send `"eventLogoFileId": "file-id"` to set/change the logo
- Send `"eventLogoFileId": ""` (empty string) to remove the logo
- Omit the field (or send `null`) to leave the logo unchanged

---

### 1.5 Upload Event Logo

Upload an image file directly as the event logo. This is the **recommended** approach — it requires only store-level permissions and handles everything in a single request.

```
POST /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/logo
Content-Type: multipart/form-data
Authorization: token YOUR_API_KEY
```

Send the image as a `file` field in multipart form data.

**Constraints:**
- File must be an image (`image/*` content type)
- Maximum file size: 1 MB
- Requires `btcpay.store.canmodifystoresettings` permission (same as other event endpoints)

**Example using curl:**

```bash
curl -X POST \
  "https://btcpay.example.com/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/logo" \
  -H "Authorization: token YOUR_API_KEY" \
  -F "file=@/path/to/event-banner.png"
```

**Response: `200 OK`** - Returns the updated `EventData` object with `eventLogoFileId` and `eventLogoUrl` populated.

```json
{
  "id": "abc123",
  "title": "Bitcoin Conference 2026",
  "eventLogoFileId": "file-abc-123",
  "eventLogoUrl": "https://btcpay.example.com/LocalStorage/file-abc-123.png",
  ...
}
```

**Errors:**
- `404` `event-not-found` - Event does not exist
- `422` `logo-upload-failed` - File validation failed (not an image, too large, etc.)

---

### 1.6 Delete Event Logo

Remove the logo from an event.

```
DELETE /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/logo
```

**Response: `200 OK`** - Returns the updated `EventData` object with `eventLogoFileId` and `eventLogoUrl` set to `null`.

---

### 1.7 Alternative: Setting Logo via File ID

If you already have a file uploaded via the BTCPay Server core Files API (`POST /api/v1/files`, requires `CanModifyServerSettings` permission), you can link it to an event by passing its ID in the create/update request:

- **Create event:** Include `"eventLogoFileId": "file-abc-123"` in the POST body
- **Update event:** Include `"eventLogoFileId": "file-abc-123"` in the PUT body
- **Remove logo:** Send `"eventLogoFileId": ""` (empty string) in the PUT body
- **Leave unchanged:** Omit the field or send `null`

**Response fields in EventData:**
- `eventLogoFileId` - the raw file ID stored in the database
- `eventLogoUrl` - the fully resolved public URL of the image (use this for display)

---

### 1.8 Delete Event

```
DELETE /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}
```

**Response: `200 OK`** - Empty body. Deletes event, all its ticket types, and all tickets (if event is past).

**Error: `422`** - `{"code": "event-has-active-tickets", "message": "Cannot delete event as there are active ticket purchases and the event is in the future"}` - Cannot delete future events with sold tickets.

---

### 1.9 Toggle Event Status (Active/Disabled)

```
PUT /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/toggle
```

No request body needed. Flips state: `Active` -> `Disabled` or `Disabled` -> `Active`.

**Response: `200 OK`** - Returns the updated `EventData` object with new `eventState`.

**Error: `422`** - `{"code": "no-ticket-types", "message": "Cannot activate event without ticket types. Create at least one ticket type first."}` - Cannot activate an event that has no ticket types.

---

## 2. TICKET TYPES

Ticket types are pricing tiers for an event (e.g. "VIP", "Standard", "Early Bird").

### 2.1 List Ticket Types

```
GET /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/ticket-types
```

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `sortBy` | string | `"Name"` | Sort field: `"Name"` or `"Price"` |
| `sortDir` | string | `"asc"` | Sort direction: `"asc"` or `"desc"` |

**Response: `200 OK`**

```json
[
  {
    "id": "tt-001",
    "eventId": "abc123",
    "name": "VIP",
    "price": 50.00,
    "description": "VIP access with backstage pass",
    "quantity": 100,
    "quantitySold": 23,
    "quantityAvailable": 77,
    "isDefault": false,
    "ticketTypeState": "Active"
  },
  {
    "id": "tt-002",
    "eventId": "abc123",
    "name": "Standard",
    "price": 20.00,
    "description": "General admission",
    "quantity": 400,
    "quantitySold": 19,
    "quantityAvailable": 381,
    "isDefault": true,
    "ticketTypeState": "Active"
  }
]
```

---

### 2.2 Get Single Ticket Type

```
GET /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/ticket-types/{ticketTypeId}
```

**Response: `200 OK`** - Single `TicketTypeData` object.

---

### 2.3 Create Ticket Type

```
POST /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/ticket-types
Content-Type: application/json
```

**Request body:**

```json
{
  "name": "VIP",
  "price": 50.00,
  "description": "VIP access with backstage pass",
  "quantity": 100,
  "isDefault": false
}
```

**Field details:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | YES | Ticket type name |
| `price` | decimal | YES | Price per ticket. Must be > 0 |
| `description` | string | no | Description of what this tier includes |
| `quantity` | int | conditional | Number of tickets available. Must be > 0 if event has maximum capacity |
| `isDefault` | bool | no | Whether this is the pre-selected default tier. First tier created is always default |

**Response: `201 Created`** - Returns the created `TicketTypeData` object.

**Validation errors (422):**
- `"Name is required"`
- `"Price cannot be zero or negative"`
- `"Quantity must be greater than zero"`
- `"Quantity specified is higher than available event capacity"`

---

### 2.4 Update Ticket Type

```
PUT /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/ticket-types/{ticketTypeId}
Content-Type: application/json
```

**Request body:** Same structure as Create.

**Response: `200 OK`** - Returns the updated `TicketTypeData` object.

**Note:** If you set `isDefault: false` and no other ticket type is default, the server forces this one to remain default (there must always be at least one default).

---

### 2.5 Delete Ticket Type

```
DELETE /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/ticket-types/{ticketTypeId}
```

**Response: `200 OK`** - Empty body.

---

### 2.6 Toggle Ticket Type Status (Active/Disabled)

```
PUT /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/ticket-types/{ticketTypeId}/toggle
```

No request body needed.

**Response: `200 OK`** - Returns the updated `TicketTypeData` object with new `ticketTypeState`.

---

## 3. TICKETS

### 3.1 List Tickets for Event

```
GET /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/tickets
```

Returns only settled (paid) tickets.

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `searchText` | string | null | Search in: `txnNumber`, `firstName`, `lastName`, `email`, `ticketNumber` |

**Response: `200 OK`**

```json
[
  {
    "id": "ticket-uuid-001",
    "eventId": "abc123",
    "ticketTypeId": "tt-002",
    "ticketTypeName": "Standard",
    "amount": 20.00,
    "firstName": "Jano",
    "lastName": "Novak",
    "email": "jano@example.com",
    "ticketNumber": "EVT-abc123-260615-xK9mP2qR",
    "txnNumber": "xK9mP2qR",
    "paymentStatus": "Settled",
    "checkedIn": false,
    "checkedInAt": null,
    "emailSent": true,
    "createdAt": "2026-02-10T14:30:00+00:00"
  }
]
```

---

### 3.2 Export Tickets as CSV

```
GET /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/tickets/export
```

**Response: `200 OK`** - Returns a `.csv` file download with `Content-Type: text/csv`.

CSV columns: `Purchase Date, Ticket Number, First Name, Last Name, Email, Ticket Tier, Amount, Currency, Attended Event`

**Error: `404`** - `{"code": "no-tickets", "message": "No settled tickets found for this event"}`

---

### 3.3 Check-in Ticket

```
POST /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/tickets/{ticketNumber}/check-in
```

The `{ticketNumber}` parameter accepts either the full `ticketNumber` (e.g. `EVT-abc123-260615-xK9mP2qR`) or just the `txnNumber` (e.g. `xK9mP2qR`).

No request body needed.

**Response: `200 OK`**

Success:
```json
{
  "success": true,
  "errorMessage": null,
  "ticket": {
    "id": "ticket-uuid-001",
    "eventId": "abc123",
    "ticketTypeId": "tt-002",
    "ticketTypeName": "Standard",
    "amount": 20.00,
    "firstName": "Jano",
    "lastName": "Novak",
    "email": "jano@example.com",
    "ticketNumber": "EVT-abc123-260615-xK9mP2qR",
    "txnNumber": "xK9mP2qR",
    "paymentStatus": "Settled",
    "checkedIn": true,
    "checkedInAt": "2026-06-15T18:05:00+00:00",
    "emailSent": true,
    "createdAt": "2026-02-10T14:30:00+00:00"
  }
}
```

Already checked in:
```json
{
  "success": false,
  "errorMessage": "Ticket previously checked in by Saturday, June 15, 2026 6:05 PM",
  "ticket": { ... }
}
```

Invalid ticket:
```json
{
  "success": false,
  "errorMessage": "Invalid ticket record specified",
  "ticket": null
}
```

---

## 4. ORDERS

### 4.1 List Orders for Event

```
GET /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/orders
```

Returns only settled (paid) orders, each with nested tickets.

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `searchText` | string | null | Search in: `invoiceId`, ticket `txnNumber`, `firstName`, `lastName`, `email` |

**Response: `200 OK`**

```json
[
  {
    "id": "order-uuid-001",
    "eventId": "abc123",
    "totalAmount": 40.00,
    "currency": "EUR",
    "invoiceId": "BTCPay-invoice-id",
    "paymentStatus": "Settled",
    "invoiceStatus": "Settled",
    "emailSent": true,
    "createdAt": "2026-02-10T14:28:00+00:00",
    "purchaseDate": "2026-02-10T14:30:00+00:00",
    "tickets": [
      {
        "id": "ticket-uuid-001",
        "eventId": "abc123",
        "ticketTypeId": "tt-002",
        "ticketTypeName": "Standard",
        "amount": 20.00,
        "firstName": "Jano",
        "lastName": "Novak",
        "email": "jano@example.com",
        "ticketNumber": "EVT-abc123-260615-xK9mP2qR",
        "txnNumber": "xK9mP2qR",
        "paymentStatus": "Settled",
        "checkedIn": false,
        "checkedInAt": null,
        "emailSent": true,
        "createdAt": "2026-02-10T14:30:00+00:00"
      },
      {
        "id": "ticket-uuid-002",
        "eventId": "abc123",
        "ticketTypeId": "tt-002",
        "ticketTypeName": "Standard",
        "amount": 20.00,
        "firstName": "Maria",
        "lastName": "Novakova",
        "email": "maria@example.com",
        "ticketNumber": "EVT-abc123-260615-pL3nQ7wR",
        "txnNumber": "pL3nQ7wR",
        "paymentStatus": "Settled",
        "checkedIn": false,
        "checkedInAt": null,
        "emailSent": true,
        "createdAt": "2026-02-10T14:30:00+00:00"
      }
    ]
  }
]
```

---

### 4.2 Send Ticket Reminder Email

```
POST /api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/orders/{orderId}/tickets/{ticketId}/send-reminder
```

No request body needed. Re-sends the ticket registration email to the ticket holder.

**Note:** `{ticketId}` here is the ticket `id` field (UUID), NOT the `ticketNumber`.

**Response: `200 OK`**

```json
{
  "success": true,
  "message": "Ticket details have been sent to the recipient via email"
}
```

**Errors:**
- `404` `order-not-found` - Order does not exist
- `404` `ticket-not-found` - Ticket does not exist within the order
- `422` `email-not-configured` - SMTP email settings not configured on the store
- `500` `email-send-failed` - Email sending failed (includes error details)

---

## Typical Workflow

The complete flow for managing an event via API:

```
1. CREATE EVENT (POST .../events)
   -> returns event with eventState: "Disabled"

2. UPLOAD LOGO (optional) (POST .../events/{id}/logo)
   -> multipart/form-data with image file
   -> only store-level API key needed

3. CREATE TICKET TYPES (POST .../events/{id}/ticket-types)
   -> create one or more pricing tiers (e.g. "VIP", "Standard")

4. ACTIVATE EVENT (PUT .../events/{id}/toggle)
   -> eventState changes to "Active"
   -> purchaseLink is now live for customers

5. MONITOR SALES
   -> GET .../events/{id}/tickets  (list sold tickets)
   -> GET .../events/{id}/orders   (list orders with tickets)
   -> GET .../events/{id}          (check ticketsSold count)

6. DAY OF EVENT - CHECK-IN
   -> POST .../events/{id}/tickets/{ticketNumber}/check-in

7. POST-EVENT
   -> GET .../events/{id}/tickets/export  (download CSV report)
   -> PUT .../events/{id}/toggle          (disable event)
```

---

## Quick Reference - All Endpoints

| Method | Endpoint | Permission | Description |
|--------|----------|------------|-------------|
| `GET` | `.../events` | View | List all events |
| `GET` | `.../events/{eventId}` | View | Get single event |
| `POST` | `.../events` | Modify | Create event |
| `PUT` | `.../events/{eventId}` | Modify | Update event |
| `DELETE` | `.../events/{eventId}` | Modify | Delete event |
| `PUT` | `.../events/{eventId}/toggle` | Modify | Toggle Active/Disabled |
| `POST` | `.../events/{eventId}/logo` | Modify | Upload event logo (multipart/form-data) |
| `DELETE` | `.../events/{eventId}/logo` | Modify | Remove event logo |
| `GET` | `.../events/{eventId}/ticket-types` | View | List ticket types |
| `GET` | `.../events/{eventId}/ticket-types/{id}` | View | Get ticket type |
| `POST` | `.../events/{eventId}/ticket-types` | Modify | Create ticket type |
| `PUT` | `.../events/{eventId}/ticket-types/{id}` | Modify | Update ticket type |
| `DELETE` | `.../events/{eventId}/ticket-types/{id}` | Modify | Delete ticket type |
| `PUT` | `.../events/{eventId}/ticket-types/{id}/toggle` | Modify | Toggle Active/Disabled |
| `GET` | `.../events/{eventId}/tickets` | View | List settled tickets |
| `GET` | `.../events/{eventId}/tickets/export` | View | Export tickets CSV |
| `POST` | `.../events/{eventId}/tickets/{ticketNumber}/check-in` | Modify | Check-in ticket |
| `GET` | `.../events/{eventId}/orders` | View | List settled orders |
| `POST` | `.../events/{eventId}/orders/{orderId}/tickets/{ticketId}/send-reminder` | Modify | Re-send ticket email |
